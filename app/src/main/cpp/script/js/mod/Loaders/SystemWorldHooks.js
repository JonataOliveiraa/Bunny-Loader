class SystemWorldHooks {
    static #custom = new WeakMap();
    static #headers = new WeakMap();

    static Track(pass) {
        SystemWorldHooks.#custom.set(pass.Native, pass);
        Hooks.Once('system.GenPass', () => {
            Terraria.GameContent.Generation.PassLegacy['void ApplyPass(GenerationProgress progress, GameConfiguration configuration)'].hook(
                (original, self, progress, config) => {
                    const custom = SystemWorldHooks.#custom.get(self);
                    if (!custom) return original(self, progress, config);
                    Safe.Run('GenPass.' + custom.Name, () => custom.Apply(progress, config));
                });
        });
    }

    static #Wrap(native) {
        const pass = SystemWorldHooks.#custom.get(native);
        if (pass) return pass;
        const wrapper = Object.create(GenPass.prototype);
        wrapper.Native = native;
        wrapper.ApplyPass = (progress, config) => native['void Apply(GenerationProgress progress, GameConfiguration configuration)'](progress, config);
        return wrapper;
    }

    static Install(cls) {
        const has = name => Hooks.Overrides(cls, ModSystem, name);
        const Main = Terraria.Main;
        if (has('PreWorldGen') || has('ModifyWorldGenTasks') || has('PostWorldGen')) Hooks.Once('system.WorldGen', () => {
            Terraria.WorldBuilding.WorldGenerator['bool GenerateWorld()'].hook((original, self) => {
                SystemLoader.Call('PreWorldGen');
                const list = self._passes;
                let tasks = [];
                Safe.Run('ModSystem.ModifyWorldGenTasks', () => {
                    for (let i = 0; i < list._size; i++) tasks.push(SystemWorldHooks.#Wrap(list._items[i]));
                    SystemLoader.Call('ModifyWorldGenTasks', tasks);
                    SystemWorldHooks.#Validate(tasks);
                    list._items = Terraria.WorldBuilding.GenPass.newArray(tasks.map(pass => pass.Native));
                    list._size = tasks.length;
                    list._version++;
                });
                try {
                    const completed = original(self);
                    if (completed) SystemLoader.Call('PostWorldGen');
                    return completed;
                } finally {
                    tasks.length = 0;
                }
            });
        });
        if (has('ModifyHardmodeTasks')) Hooks.Once('system.Hardmode', () => {
            Terraria.WorldGen['void initializeHardMode()'].hook(original => {
                const tasks = [new PassLegacy('Hardmode Conversion', () => original())];
                SystemLoader.Call('ModifyHardmodeTasks', tasks);
                try { SystemWorldHooks.#Validate(tasks); }
                catch (error) { Safe.Report('ModSystem.ModifyHardmodeTasks', error); return original(); }
                for (const pass of tasks) Safe.Run('Hardmode.' + pass.Name, () => pass.Apply(null, null));
            });
        });
        if (has('SaveWorldHeader') || has('CanWorldBePlayed')) Hooks.Once('system.WorldHeaders', () => {
            bl.defineMethod(Terraria.IO.WorldFileData, 'TryGetHeaderData', function (system, result) {
                return SystemWorldHooks.TryGetHeaderData(this, system, result);
            });
            Terraria.IO.WorldFile['WorldFileData GetAllMetadata(string file, bool cloudSave)'].hook((original, file, cloud) => {
                const data = original(file, cloud);
                if (data) SystemWorldHooks.#headers.set(data, cloud ? {} : SystemWorldHooks.#Read(file + '.bl.header.json'));
                return data;
            });
        });
        if (has('CanWorldBePlayed')) Hooks.Once('system.WorldPlayable', () => {
            bl.classOf('', 'GUIWorldSelectMenu')['bool CanWorldBePlayed(WorldFileData world)'].hook((original, self, world) => {
                return original(self, world) && !SystemWorldHooks.Rejection(Main.ActivePlayerFileData, world);
            });
            bl.classOf('', 'GUIWorldSelectMenu')['void PlayWorldCheck()'].hook((original, self) => {
                const index = self.SelectedItem, worlds = self.SortedWorldData;
                if (index >= 0 && index < worlds._size) {
                    const blocked = SystemWorldHooks.Rejection(Main.ActivePlayerFileData, worlds._items[index]);
                    if (blocked) { bl.error(blocked); return; }
                }
                original(self);
            });
        });
    }

    static #Validate(tasks) {
        for (const pass of tasks) {
            if (!(pass instanceof GenPass) || !pass.Native || !Number.isFinite(pass.Weight) || pass.Weight < 0) {
                throw new TypeError('ModSystem: a lista de geração deve conter GenPass com peso válido');
            }
        }
    }

    static Rejection(player, world) {
        if (!player || !world) return '';
        for (const entry of SystemLoader.Entries('CanWorldBePlayed')) {
            try {
                if (entry.instance.CanWorldBePlayed(player, world) !== false) continue;
            } catch (error) { Safe.Report(entry.label, error); continue; }
            try {
                const message = entry.instance.WorldCanBePlayedRejectionMessage(player, world);
                return typeof message === 'string' && message.trim() ? message : 'Este mod não permite a combinação de personagem e mundo.';
            } catch (error) { Safe.Report(SystemLoader.KeyOf(entry.instance) + '.WorldCanBePlayedRejectionMessage', error); }
            return 'Este mod não permite a combinação de personagem e mundo.';
        }
        return '';
    }

    static #Read(file) {
        try {
            const text = bl.file.read(file);
            const data = text ? JSON.parse(text) : {};
            return data && typeof data === 'object' && !Array.isArray(data) ? data : {};
        } catch (error) { Safe.Report('ModSystem.Header:' + file, error); return {}; }
    }

    static TryGetHeaderData(world, system, result) {
        if (!result || typeof result !== 'object') throw new TypeError('TryGetHeaderData: use Ref');
        const instance = typeof system === 'function' ? SystemLoader.List.find(s => s.constructor === system) : system;
        const key = SystemLoader.KeyOf(instance);
        let headers = SystemWorldHooks.#headers.get(world);
        if (!headers) {
            headers = world.IsCloudSave || !world.Path ? {} : SystemWorldHooks.#Read(world.Path + '.bl.header.json');
            SystemWorldHooks.#headers.set(world, headers);
        }
        const data = key && headers[key];
        result.value = TagCompound.from(data);
        return !!data;
    }

    static SaveHeader() {
        if (!SystemLoader.Entries('SaveWorldHeader').length) return;
        const Main = Terraria.Main, world = Main.ActiveWorldFileData;
        const path = Main.worldPathName;
        if (!path) return;
        const file = path + '.bl.header.json', headers = SystemWorldHooks.#Read(file);
        for (const entry of SystemLoader.Entries('SaveWorldHeader')) {
            const tag = new TagCompound(), key = SystemLoader.KeyOf(entry.instance);
            SystemLoader.Invoke(entry.instance, 'SaveWorldHeader', tag);
            if (tag.Count) headers[key] = tag;
            else delete headers[key];
        }
        Safe.Run('ModSystem.SaveWorldHeader', () => {
            if (Object.keys(headers).length) bl.file.write(file, JSON.stringify(headers));
            else bl.file.delete(file);
            if (world) SystemWorldHooks.#headers.set(world, headers);
        });
    }
}
