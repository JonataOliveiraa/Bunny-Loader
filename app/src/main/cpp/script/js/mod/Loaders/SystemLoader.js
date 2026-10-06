// Cada método de mundo só ganha hook se algum ModSystem o escreveu.
class SystemLoader {
    static List = [];
    static #keys = new Map();   // instância -> 'uuid/Classe', a chave no arquivo
    static #methods = new Map();
    static #unloaded = false;

    static Add(inst) {
        SystemLoader.List.push(inst);
        SystemLoader.#keys.set(inst, (bl.mod ? bl.mod.uuid : 'sem-mod') + '/' + inst.constructor.name);
        SystemLoader.#methods.clear();
    }

    static KeyOf(inst) {
        return SystemLoader.#keys.get(inst);
    }

    static Each(method, fn) {
        for (const entry of SystemLoader.Entries(method)) {
            try { fn(entry.instance); }
            catch (error) { Safe.Report(entry.label, error); }
        }
    }

    static Entries(method) {
        let entries = SystemLoader.#methods.get(method);
        if (!entries) {
            entries = SystemLoader.List.filter(s => Hooks.Overrides(s.constructor, ModSystem, method))
                .map(instance => ({ instance, label: instance.constructor.name + '.' + method }));
            SystemLoader.#methods.set(method, entries);
        }
        return entries;
    }

    static Invoke(instance, method, ...args) {
        try { return instance[method](...args); }
        catch (error) { Safe.Report(instance.constructor.name + '.' + method, error); }
    }

    static Call(method, ...args) {
        for (const entry of SystemLoader.Entries(method)) {
            try { entry.instance[method](...args); }
            catch (error) { Safe.Report(entry.label, error); }
        }
    }

    static Any(method, ...args) {
        let result = false;
        for (const entry of SystemLoader.Entries(method)) {
            try { if (entry.instance[method](...args) === true) result = true; }
            catch (error) { Safe.Report(entry.label, error); }
        }
        return result;
    }

    static Unload() {
        if (SystemLoader.#unloaded) return;
        SystemLoader.#unloaded = true;
        SystemLoader.Call('OnModUnload');
    }

    static Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModSystem, name);
        const Main = Terraria.Main;
        const WorldGen = Terraria.WorldGen;
        const WorldFile = Terraria.IO.WorldFile;
        SystemUpdateHooks.Install(cls);
        SystemDrawHooks.Install(cls);
        SystemWorldHooks.Install(cls);
        SystemNetworkHooks.Install(cls);
        if (has('OnLocalizationsLoaded')) Hooks.Once('system.Localizations', () => {
            LocalizationLoader.WantLoaded(() => SystemLoader.Call('OnLocalizationsLoaded'));
        });
        if (has('OnModUnload')) Hooks.Once('system.ModUnload', () => {
            bl.__unloadMods = () => SystemLoader.Unload();
            Main['void QuitGame()'].hook((original, self) => {
                SystemLoader.Unload();
                return original(self);
            });
        });

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity();

        // No cliente de multijogador o mundo chega pela rede, sem LoadWorld: o
        // clearWorld marca a entrada.
        if (has('ClearWorld') || has('OnWorldLoad')) Hooks.Once('system.ClearWorld', () => {
            WorldGen['void clearWorld()'].hook((original) => {
                original();

                SystemLoader.Call('ClearWorld');
                if (Main.netMode === 1) SystemLoader.Call('OnWorldLoad');
            });
        });

        if (has('OnWorldLoad') || has('LoadWorldData') || has('PostWorldLoad') || has('CanWorldBePlayed')) Hooks.Once('system.LoadWorld', () => {
            WorldFile['void LoadWorld(bool canCheckFileState)'].hook((original, check) => {
                const blocked = SystemWorldHooks.Rejection(Main.ActivePlayerFileData, Main.ActiveWorldFileData);
                if (blocked) { bl.error(blocked); return; }
                original(check);

                SystemLoader.Call('OnWorldLoad');
                SystemLoader.#Load();
                SystemLoader.Call('PostWorldLoad');
            });
        });

        if (has('SaveWorldData') || has('SaveWorldHeader')) Hooks.Once('system.SaveWorld', () => {
            WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'].hook((original, cloud, reset) => {
                original(cloud, reset);
                if (!cloud) {
                    SystemLoader.#Save();
                    SystemWorldHooks.SaveHeader();
                }
            });
        });

        if (has('PreSaveAndQuit')) Hooks.Once('system.PreSaveAndQuit', () => {
            WorldGen['void SaveAndQuit()'].hook((original) => {
                SystemLoader.Call('PreSaveAndQuit');
                original();
            });
        });

        if (has('OnWorldUnload')) Hooks.Once('system.Unload', () => {
            WorldGen['void SaveAndQuitCallBack(object threadContext)'].hook((original, context) => {
                original(context);
                SystemLoader.Call('OnWorldUnload');
            });
        });

        if (has('PreUpdateWorld') || has('PostUpdateWorld')) Hooks.Once('system.UpdateWorld', () => {
            WorldGen['void UpdateWorld()'].hook((original) => {
                SystemLoader.Call('PreUpdateWorld');
                original();
                SystemLoader.Call('PostUpdateWorld');
            });
        });

        if (has('PreUpdateTime') || has('PostUpdateTime')) Hooks.Once('system.UpdateTime', () => {
            Main['void UpdateTime()'].hook((original) => {
                SystemLoader.Call('PreUpdateTime');
                original();
                SystemLoader.Call('PostUpdateTime');
            });
        });

        // Só a varredura do jogador local (Main.PlayerSceneMetrics). O
        // tModLoader chama em toda, e a dos pilares e a da câmera também
        // sobrescreveriam a contagem guardada pelo mod.
        if (has('ResetNearbyTileEffects') || has('TileCountsAvailable')) Hooks.Once('system.TileCounts', () => {
            const SceneMetrics = Terraria.SceneMetrics;
            const isPlayers = (self) => !Main.gameMenu && self === Main.PlayerSceneMetrics;

            SceneMetrics['void Reset()'].hook((original, self) => {
                original(self);
                if (isPlayers(self)) SystemLoader.Call('ResetNearbyTileEffects');
            });
            SceneMetrics['void AggregateTileCounts()'].hook((original, self) => {
                original(self);
                if (!isPlayers(self)) return;

                const counts = self._tileCounts;
                SystemLoader.Call('TileCountsAvailable', counts);
            });

            // Fora do mundo não há varredura: a contagem do mundo anterior
            // valeria no novo até a primeira dele (alguns quadros).
            const reset = () => SystemLoader.Call('ResetNearbyTileEffects');
            WorldGen['void SaveAndQuit()'].hook((original) => {
                reset();
                original();
            });
            WorldGen['void clearWorld()'].hook((original) => {
                original();
                reset();
            });
        });

    }

    // <mundo>.wld.bl.json
    static #WorldFile() {
        const path = Terraria.Main.worldPathName;
        return path ? path + '.bl.json' : null;
    }

    static #Read(file) {
        const text = bl.file.read(file);
        if (!text) return {};

        try {
            return JSON.parse(text) || {};
        } catch (e) {
            bl.error('ModSystem: ' + file + ' is broken (' + e + '); the world data was ignored');
            return {};
        }
    }

    static #Save() {
        const file = SystemLoader.#WorldFile();
        if (!file) return;

        const all = SystemLoader.#Read(file);
        for (const s of SystemLoader.List) {
            if (!Hooks.Overrides(s.constructor, ModSystem, 'SaveWorldData')) continue;

            const key = SystemLoader.KeyOf(s);
            const tag = new TagCompound();
            Safe.Run(s.constructor.name + '.SaveWorldData', () => s.SaveWorldData(tag));

            if (Object.keys(tag).length) all[key] = tag;
            else delete all[key];
        }

        Safe.Run('ModSystem: gravar ' + file, () => {
            if (Object.keys(all).length) bl.file.write(file, JSON.stringify(all));
            else bl.file.delete(file);
        });
    }

    // Mundo sem os dados do mod: tag vazia, para voltar ao "nada aconteceu".
    static #Load() {
        const file = SystemLoader.#WorldFile();
        const all = file ? SystemLoader.#Read(file) : {};

        for (const s of SystemLoader.List) {
            if (!Hooks.Overrides(s.constructor, ModSystem, 'LoadWorldData')) continue;

            const data = all[SystemLoader.KeyOf(s)];
            Safe.Run(s.constructor.name + '.LoadWorldData', () => s.LoadWorldData(TagCompound.from(data)));
        }
    }
}
