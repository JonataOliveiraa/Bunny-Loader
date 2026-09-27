// As regras de drop que valem para TODO NPC. Também é a classe do jeito do
// ExMod: `class X extends GlobalLoot { ModifyGlobalLoot() {...} }`, com
// RegisterToNPC/RemoveFromNPC/GetRulesForNPCID mexendo na tabela de um NPC.
class GlobalLoot {
    constructor() {
        this.__changed = [];
    }

    get itemDropDatabase() { return Terraria.Main.ItemDropsDB; }

    Get() {
        return NPCLoader.Rules(Terraria.Main.ItemDropsDB._globalEntries);
    }

    Add(rule) {
        Terraria.Main.ItemDropsDB['IItemDropRule RegisterToGlobal(IItemDropRule entry)'](rule);
        return rule;
    }

    Remove(rule) {
        Terraria.Main.ItemDropsDB._globalEntries['bool Remove(IItemDropRule item)'](rule);
        return rule;
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

    ModifyGlobalLoot() {}

    GetRulesForNPCID(id) {
        return Terraria.Main.ItemDropsDB['GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](id, false);
    }

    RegisterToNPC(id, rule) {
        new NPCLoot(id).Add(rule);
        this.__changed.push(id);
        return rule;
    }

    RemoveFromNPC(id, rule) {
        new NPCLoot(id).Remove(rule);
        this.__changed.push(id);
        return rule;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof GlobalLoot)) {
            throw new TypeError('GlobalLoot.register(Classe): passe a classe, que estende GlobalLoot');
        }

        const inst = new cls(Terraria.Main.ItemDropsDB);
        Templates.Adopt(cls, inst);
        if (!inst.__changed) inst.__changed = [];

        GlobalLootLoader.List.push(inst);
        GlobalLootLoader.Schedule();
        return inst;
    }
}
