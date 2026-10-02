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

    static Hook(cls, type) {
        const N = Terraria.NPC;
        const has = (name) => Hooks.Overrides(cls, ModNPC, name);
        const of = NPCLoader.Of;
        // Os hooks de um método só entram no JS para os tipos cuja classe o
        // escreve (os outros NPCs de mod passam direto, como os do jogo).
        const marked = (key, ...methods) => {
            if (methods.some(has)) bl.hookMarks.set(key, type);
            return { minType: FIRST_NPC, marks: key };
        };

        if (has('SendExtraAI') || has('ReceiveExtraAI')) ModNet.InstallEntity();

        TownNPCLoader.Hook(cls);

        const chat = marked('npc.GetChat', 'GetChat');
        if (has('GetChat')) Hooks.Once('npc.GetChat', () => {
            N['string GetChat()'].hook((original, npc) => {
                const chat = original(npc);
                const m = of(npc);
                const text = m ? Safe.Run(m.constructor.name + '.GetChat', () => m.GetChat(npc)) : undefined;
                return typeof text === 'string' ? text : chat;
            }, chat);
        });

        const ai = marked('npc.AI', 'PreAI', 'AI', 'PostAI');
        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('npc.AI', () => {
            N['void AI()'].hook((original, npc) => {
                const m = of(npc);
                if (!m) return original(npc);

                // Sem closure nem rótulo montado por chamada: só o que a classe escreve.
                const plan = NPCLoader.#PlanOf(m);
                let go = true;
                if (plan.PreAI) try { go = m.PreAI(npc) !== false; } catch (e) { Safe.Report(plan.PreAI, e); }
                if (go) {
                    original(npc);
                    if (plan.AI) try { m.AI(npc); } catch (e) { Safe.Report(plan.AI, e); }
                }
                if (plan.PostAI) try { m.PostAI(npc); } catch (e) { Safe.Report(plan.PostAI, e); }
            }, ai);
        });

        const frame = marked('npc.FindFrame', 'FindFrame');
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
            }, frame);
        });

        const active = marked('npc.CheckActive', 'CheckActive');
        if (has('CheckActive')) Hooks.Once('npc.CheckActive', () => {
            N['void CheckActive()'].hook((original, npc) => {
                const m = of(npc);
                if (m && Safe.Run(m.constructor.name + '.CheckActive', () => m.CheckActive(npc)) === false) return;

                original(npc);
            }, active);
        });

        // No cliente de multijogador o drop é do servidor: PreKill e OnKill só lá.
        const kill = marked('npc.Kill', 'PreKill', 'OnKill');
        if (has('PreKill') || has('OnKill')) Hooks.Once('npc.Kill', () => {
            N['void NPCLoot()'].hook((original, npc) => {
                const m = of(npc);
                if (!m || Terraria.Main.netMode === 1) return original(npc);

                const n = m.constructor.name;
                if (Safe.Run(n + '.PreKill', () => m.PreKill(npc)) === false) return;

                original(npc);
                Safe.Run(n + '.OnKill', () => m.OnKill(npc));
            }, kill);
        });

        if (has('SpawnChance')) SpawnLoader.InstallPool();
    }

    // Os rótulos do log de erro da AI, montados uma vez por classe.
    static #plans = new Map();
    static #PlanOf(m) {
        const cls = m.constructor;
        let plan = NPCLoader.#plans.get(cls);
        if (plan) return plan;

        plan = {};
        for (const name of ['PreAI', 'AI', 'PostAI']) {
            plan[name] = Hooks.Overrides(cls, ModNPC, name) ? cls.name + '.' + name : null;
        }
        NPCLoader.#plans.set(cls, plan);
        return plan;
    }
}
