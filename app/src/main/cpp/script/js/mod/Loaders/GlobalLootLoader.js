class GlobalLootLoader {
    static List = [];
    static #scheduled = false;

    static Schedule() {
        if (GlobalLootLoader.#scheduled) return;

        GlobalLootLoader.#scheduled = true;
        Ready.Add(GlobalLootLoader.#Apply);
    }

    // O ModifyNPCLoot de cada Global em cada tipo de NPC (com a amostra do
    // jogo), o ModifyGlobalLoot, e o Bestiário dos NPCs cuja tabela mudou.
    static #Apply() {
        const changed = [];
        GlobalLootLoader.#ModifyEachNPC(changed);

        const everyone = new GlobalLoot();
        for (const g of globalNPCs.list) {
            if (Hooks.Overrides(g.constructor, GlobalNPC, 'ModifyGlobalLoot')) {
                Safe.Run(g.constructor.name + '.ModifyGlobalLoot', () => g.ModifyGlobalLoot(everyone));
            }
        }

        for (const loot of GlobalLootLoader.List) {
            Safe.Run(loot.constructor.name + '.ModifyGlobalLoot', () => loot.ModifyGlobalLoot());
            changed.push(...loot.__changed);
        }

        BestiaryLoader.RefreshDrops(changed);
    }

    static #ModifyEachNPC(changed) {
        const looters = globalNPCs.list.filter((g) => Hooks.Overrides(g.constructor, GlobalNPC, 'ModifyNPCLoot'));
        if (!looters.length) return;

        const samples = Terraria.ID.ContentSamples.NpcsByNetId;
        const modify = (type) => {
            if (!samples.ContainsKey(type)) return;

            const npc = samples.get_Item(type);
            const loot = new NPCLoot(type);
            for (const g of looters) {
                const n = g.constructor.name;
                if (g.__conditional && !Safe.Run(n + '.AppliesToEntity', () => g.AppliesToEntity(npc, false))) continue;

                Safe.Run(n + '.ModifyNPCLoot', () => g.ModifyNPCLoot(npc, loot));
            }
            if (loot.changed) changed.push(type);
        };

        for (let type = -65; type < FIRST_NPC; type++) modify(type);
        for (let type = FIRST_NPC; samples.ContainsKey(type); type++) modify(type);
    }
}
