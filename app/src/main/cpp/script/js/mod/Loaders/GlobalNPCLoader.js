// O que só o GlobalNPC tem (SetDefaults de todo NPC, a IA, o HitEffect, o
// saque, o spawn, a rede e as lojas). O resto vai pelos hooks do NPCLoader,
// os mesmos do ModNPC.
class GlobalNPCLoader {
    static Hook(cls) {
        const N = Terraria.NPC;
        const registry = globalNPCs;
        const has = (name) => Hooks.Overrides(cls, GlobalNPC, name);

        if (has('NetSend') || has('NetReceive')) ModNet.InstallEntity(23);
        NPCLoader.HookGlobal(cls);

        if (has('SetDefaults') || registry.cached) Hooks.Once('gnpc.SetDefaults', () => {
            N['void SetDefaults(int Type, NPCSpawnParams spawnparams)'].hook((original, npc, type, params) => {
                original(npc, type, params);
                if (!(npc.type > 0)) return;

                if (registry.cached) registry.Attach(npc, true);
                registry.Each(npc, 'SetDefaults', (g) => g.SetDefaults(npc));
            });
        });

        if (has('PreAI') || has('AI') || has('PostAI')) NPCLoader.InstallAI(true);

        if (has('HitEffect')) Hooks.Once('gnpc.HitEffect', () => {
            N['void HitEffect(int hitDirection, double dmg)'].hook((original, npc, dir, dmg) => {
                original(npc, dir, dmg);
                registry.Each(npc, 'HitEffect', (g) => g.HitEffect(npc, dir, dmg));
            });
        });

        if (['SetupShop', 'ModifyShop', 'ModifyActiveShop'].some(has)) NPCShop.Install();
        if (has('SetupTravelShop')) NPCShop.InstallTravel();

        if (has('ModifyNPCLoot') || has('ModifyGlobalLoot')) GlobalLootLoader.Schedule();

        if (has('EditSpawnFlags')) SpawnLoader.InstallFlags();
        if (has('EditSpawnPool')) SpawnLoader.InstallPool();
        if (has('EditSpawnRate')) SpawnLoader.InstallRate();
        if (has('EditSpawnRange')) SpawnLoader.InstallRange();
        if (has('EditSpawnInfo')) SpawnLoader.InstallInfo();
    }
}
