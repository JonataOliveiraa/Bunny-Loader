class NPCLoot {
    constructor(type) {
        this.type = type;
        this.changed = false;
    }

    Add(rule) {
        Terraria.Main.ItemDropsDB['IItemDropRule RegisterToNPC(int type, IItemDropRule entry)'](this.type, rule);
        this.changed = true;
        return rule;
    }

    Remove(rule) {
        Terraria.Main.ItemDropsDB['IItemDropRule RemoveFromNPC(int type, IItemDropRule entry)'](this.type, rule);
        this.changed = true;
        return rule;
    }

    // Sem as regras globais.
    Get() {
        const rules = Terraria.Main.ItemDropsDB['GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](this.type, false);
        return NPCLoader.Rules(rules);
    }

    RemoveWhere(predicate) {
        let removed = 0;
        for (const rule of this.Get()) {
            if (!predicate(rule)) continue;

            this.Remove(rule);
            removed++;
        }
        return removed;
    }
}
