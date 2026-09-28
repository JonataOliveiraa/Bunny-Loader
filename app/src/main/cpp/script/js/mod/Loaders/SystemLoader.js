// Cada método de mundo só ganha hook se algum ModSystem o escreveu.
class SystemLoader {
    static List = [];
    static #keys = new Map();   // instância -> 'uuid/Classe', a chave no arquivo

    static Add(inst) {
        SystemLoader.List.push(inst);
        SystemLoader.#keys.set(inst, (bl.mod ? bl.mod.uuid : 'sem-mod') + '/' + inst.constructor.name);
    }

    static KeyOf(inst) {
        return SystemLoader.#keys.get(inst);
    }

    static Each(method, fn) {
        for (const s of SystemLoader.List) {
            if (Hooks.Overrides(s.constructor, ModSystem, method)) Safe.Run(s.constructor.name + '.' + method, () => fn(s));
        }
    }

    static Hook(cls) {
        const has = (name) => Hooks.Overrides(cls, ModSystem, name);
        const each = SystemLoader.Each;
        const Main = Terraria.Main;
        const WorldGen = Terraria.WorldGen;
        const WorldFile = Terraria.IO.WorldFile;

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity();

        // No cliente de multijogador o mundo chega pela rede, sem LoadWorld: o
        // clearWorld marca a entrada.
        if (has('ClearWorld') || has('OnWorldLoad')) Hooks.Once('system.ClearWorld', () => {
            WorldGen['void clearWorld()'].hook((original) => {
                original();

                each('ClearWorld', (s) => s.ClearWorld());
                if (Main.netMode === 1) each('OnWorldLoad', (s) => s.OnWorldLoad());
            });
        });

        if (has('OnWorldLoad') || has('LoadWorldData') || has('PostWorldLoad')) Hooks.Once('system.LoadWorld', () => {
            WorldFile['void LoadWorld(bool canCheckFileState)'].hook((original, check) => {
                original(check);

                each('OnWorldLoad', (s) => s.OnWorldLoad());
                SystemLoader.#Load();
                each('PostWorldLoad', (s) => s.PostWorldLoad());
            });
        });

        if (has('SaveWorldData')) Hooks.Once('system.SaveWorld', () => {
            WorldFile['void InternalSaveWorld(bool useCloudSaving, bool resetTime)'].hook((original, cloud, reset) => {
                original(cloud, reset);
                if (!cloud) SystemLoader.#Save();
            });
        });

        if (has('PreSaveAndQuit')) Hooks.Once('system.PreSaveAndQuit', () => {
            WorldGen['void SaveAndQuit()'].hook((original) => {
                each('PreSaveAndQuit', (s) => s.PreSaveAndQuit());
                original();
            });
        });

        if (has('OnWorldUnload')) Hooks.Once('system.Unload', () => {
            WorldGen['void SaveAndQuitCallBack(object threadContext)'].hook((original, context) => {
                original(context);
                each('OnWorldUnload', (s) => s.OnWorldUnload());
            });
        });

        if (has('PreUpdateWorld') || has('PostUpdateWorld')) Hooks.Once('system.UpdateWorld', () => {
            WorldGen['void UpdateWorld()'].hook((original) => {
                each('PreUpdateWorld', (s) => s.PreUpdateWorld());
                original();
                each('PostUpdateWorld', (s) => s.PostUpdateWorld());
            });
        });

        if (has('PreUpdateTime') || has('PostUpdateTime')) Hooks.Once('system.UpdateTime', () => {
            Main['void UpdateTime()'].hook((original) => {
                each('PreUpdateTime', (s) => s.PreUpdateTime());
                original();
                each('PostUpdateTime', (s) => s.PostUpdateTime());
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
                if (isPlayers(self)) each('ResetNearbyTileEffects', (s) => s.ResetNearbyTileEffects());
            });
            SceneMetrics['void AggregateTileCounts()'].hook((original, self) => {
                original(self);
                if (!isPlayers(self)) return;

                const counts = self._tileCounts;
                each('TileCountsAvailable', (s) => s.TileCountsAvailable(counts));
            });

            // Fora do mundo não há varredura: a contagem do mundo anterior
            // valeria no novo até a primeira dele (até 5 quadros).
            const reset = () => each('ResetNearbyTileEffects', (s) => s.ResetNearbyTileEffects());
            WorldGen['void SaveAndQuit()'].hook((original) => {
                reset();
                original();
            });
            WorldGen['void clearWorld()'].hook((original) => {
                original();
                reset();
            });
        });

        if (has('PostUpdateEverything')) Hooks.Once('system.UpdateEverything', () => {
            Main['void DoUpdateInWorld(Stopwatch sw)'].hook((original, self, sw) => {
                original(self, sw);
                each('PostUpdateEverything', (s) => s.PostUpdateEverything());
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
            bl.log('ModSystem: ' + file + ' esta quebrado (' + e + '); os dados do mundo foram ignorados');
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
