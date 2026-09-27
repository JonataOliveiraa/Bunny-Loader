class NPCLoader {
    static ByType = new Map();
    static Spawnable = [];   // os que têm SpawnChance

    static Of(npc) {
        return Entities.InstanceOf(npc, 'ModNPC', NPCLoader.ByType);
    }

    // Uma List<IItemDropRule> do jogo num array.
    static Rules(list) {
        const out = [];
        const count = list ? list.Count : 0;
        for (let i = 0; i < count; i++) out.push(list.get_Item(i));
        return out;
    }

    static Hook(cls) {
        const N = Terraria.NPC;
        const has = (name) => Hooks.Overrides(cls, ModNPC, name);
        const self = { minType: FIRST_NPC };
        const of = NPCLoader.Of;

        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity();

        TownNPCLoader.Hook(cls);

        if (has('GetChat')) Hooks.Once('npc.GetChat', () => {
            N['string GetChat()'].hook((original, npc) => {
                const chat = original(npc);
                const m = of(npc);
                const text = m ? Safe.Run(m.constructor.name + '.GetChat', () => m.GetChat(npc)) : undefined;
                return typeof text === 'string' ? text : chat;
            }, self);
        });

        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('npc.AI', () => {
            N['void AI()'].hook((original, npc) => {
                const m = of(npc);
                if (!m) return original(npc);

                const n = m.constructor.name;
                if (Safe.Run(n + '.PreAI', () => m.PreAI(npc)) !== false) {
                    original(npc);
                    Safe.Run(n + '.AI', () => m.AI(npc));
                }
                Safe.Run(n + '.PostAI', () => m.PostAI(npc));
            }, self);
        });

        if (has('FindFrame')) Hooks.Once('npc.FindFrame', () => {
            const heights = new Map();

            N['void FindFrame()'].hook((original, npc) => {
                const m = of(npc);
                if (!m || !Hooks.Overrides(m.constructor, ModNPC, 'FindFrame')) return original(npc);

                let height = heights.get(npc.type);
                if (height === undefined) {
                    const texture = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                    const frames = Math.max(1, Terraria.Main.npcFrameCount[npc.type]);
                    height = texture ? Math.floor(texture.Height / frames) : 0;
                    heights.set(npc.type, height);
                }
                Safe.Run(m.constructor.name + '.FindFrame', () => m.FindFrame(npc, height));
            }, self);
        });

        if (has('CheckActive')) Hooks.Once('npc.CheckActive', () => {
            N['void CheckActive()'].hook((original, npc) => {
                const m = of(npc);
                if (m && Safe.Run(m.constructor.name + '.CheckActive', () => m.CheckActive(npc)) === false) return;

                original(npc);
            }, self);
        });

        // No cliente de multijogador o drop é do servidor: PreKill e OnKill só lá.
        if (has('PreKill') || has('OnKill')) Hooks.Once('npc.Kill', () => {
            N['void NPCLoot()'].hook((original, npc) => {
                const m = of(npc);
                if (!m || Terraria.Main.netMode === 1) return original(npc);

                const n = m.constructor.name;
                if (Safe.Run(n + '.PreKill', () => m.PreKill(npc)) === false) return;

                original(npc);
                Safe.Run(n + '.OnKill', () => m.OnKill(npc));
            }, self);
        });

        if (has('SpawnChance')) Hooks.Once('npc.Spawn', NPCLoader.#HookNaturalSpawn);
    }

    // O SpawnNPC do jogo roda; se nasceu um NPC, sorteia entre ele (peso 1) e
    // os de mod (peso = SpawnChance). Um de mod toma o lugar, no mesmo ponto.
    static #HookNaturalSpawn() {
        const Main = Terraria.Main;

        Terraria.NPC['void SpawnNPC()'].hook((original) => {
            const slot = bl.npcs.freeSlot();
            original();
            if (slot < 0) return;

            const npc = Main.npc[slot];
            if (!npc.active || npc.townNPC || npc.boss) return;

            const x = npc.Center.X, y = npc.Bottom.Y;
            const info = new NPCSpawnInfo(x, y, Main.player[Main.myPlayer]);
            const pool = [];
            let total = 1;
            for (const m of NPCLoader.Spawnable) {
                const weight = Number(Safe.Run(m.constructor.name + '.SpawnChance', () => m.SpawnChance(info))) || 0;
                if (weight > 0) {
                    pool.push([m, weight]);
                    total += weight;
                }
            }
            if (!pool.length) return;

            let r = Math.random() * total;
            if (r < 1) return;   // ficou o do jogo

            r -= 1;
            for (const [m, weight] of pool) {
                if (r < weight) {
                    npc.active = false;
                    Safe.Run(m.constructor.name + '.SpawnNPC', () => m.SpawnNPC(x, y));
                    return;
                }
                r -= weight;
            }
        });
    }
}
