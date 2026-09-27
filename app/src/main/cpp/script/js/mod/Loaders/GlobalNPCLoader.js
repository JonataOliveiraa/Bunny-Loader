class GlobalNPCLoader {
    static Hook(cls) {
        const N = Terraria.NPC;
        const registry = globalNPCs;
        const has = (name) => Hooks.Overrides(cls, GlobalNPC, name);

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity();

        if (has('SetDefaults') || registry.cached) Hooks.Once('gnpc.SetDefaults', () => {
            N['void SetDefaults(int Type, NPCSpawnParams spawnparams)'].hook((original, npc, type, params) => {
                original(npc, type, params);
                if (!(npc.type > 0)) return;

                if (registry.cached) registry.Attach(npc, true);
                registry.Each(npc, 'SetDefaults', (g) => g.SetDefaults(npc));
            });
        });

        // Só quem cria o NPC (sozinho ou servidor): no cliente ele chega pela rede.
        if (has('OnSpawn')) Hooks.Once('gnpc.OnSpawn', () => {
            N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'].hook(
                (original, source, x, y, type, start, ai0, ai1, ai2, ai3, target) => {
                    const i = original(source, x, y, type, start, ai0, ai1, ai2, ai3, target);
                    if (i < 0 || i >= 200) return i;

                    const npc = Terraria.Main.npc[i];
                    if (npc.active) registry.Each(npc, 'OnSpawn', (g) => g.OnSpawn(npc, source));
                    return i;
                });
        });

        if (has('ResetEffects')) Hooks.Once('gnpc.ResetEffects', () => {
            N['void UpdateNPC(int i)'].hook((original, npc, i) => {
                if (npc.active) registry.Each(npc, 'ResetEffects', (g) => g.ResetEffects(npc));
                original(npc, i);
            });
        });

        if (has('PreAI') || has('AI') || has('PostAI')) Hooks.Once('gnpc.AI', () => {
            N['void AI()'].hook((original, npc) => {
                if (registry.All(npc, 'PreAI', (g) => g.PreAI(npc))) {
                    original(npc);
                    registry.Each(npc, 'AI', (g) => g.AI(npc));
                }
                registry.Each(npc, 'PostAI', (g) => g.PostAI(npc));
            });
        });

        if (has('HitEffect')) Hooks.Once('gnpc.HitEffect', () => {
            N['void HitEffect(int hitDirection, double dmg)'].hook((original, npc, dir, dmg) => {
                original(npc, dir, dmg);
                registry.Each(npc, 'HitEffect', (g) => g.HitEffect(npc, dir, dmg));
            });
        });

        if (has('OnHitByItem')) HitLoader.ItemHitsNPC();

        if (has('OnHitByProjectile')) HitLoader.ProjectileHitsNPC();

        if (has('PreKill') || has('OnKill')) Hooks.Once('gnpc.Kill', () => {
            N['void NPCLoot()'].hook((original, npc) => {
                if (Terraria.Main.netMode === 1) return original(npc);   // o drop é do servidor
                if (!registry.All(npc, 'PreKill', (g) => g.PreKill(npc))) return undefined;

                original(npc);
                registry.Each(npc, 'OnKill', (g) => g.OnKill(npc));
                return undefined;
            });
        });

        if (has('GetChat')) Hooks.Once('gnpc.GetChat', () => {
            N['string GetChat()'].hook((original, npc) => {
                const chat = new Ref(original(npc));
                registry.Each(npc, 'GetChat', (g) => g.GetChat(npc, chat));
                return String(chat.value);
            });
        });

        if (has('ModifyNPCLoot') || has('ModifyGlobalLoot')) GlobalLootLoader.Schedule();
    }
}
