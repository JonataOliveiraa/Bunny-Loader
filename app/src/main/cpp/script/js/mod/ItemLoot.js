// O que sai de um item aberto (bolsa de chefe, caixa), como o ItemLoot do
// tModLoader. O jogo nao tem tabela de drops por item (o ItemDropDatabase so
// conhece NPC): as regras ficam aqui, por tipo, e o ItemLoader as roda pelo
// resolvedor do jogo quando o item e aberto. Valem as regras do ItemDropRule
// (Common, NotScalingWithLuck, OneFromOptions, LeadingConditionRule com
// Chains.OnSuccess...) e o ItemDropRule.CoinsBasedOnNPCValue / Coins.
class ItemLoot {
    static #byType = new Map();

    constructor(type) {
        this.type = type;
        this.rules = ItemLoot.#RulesOf(type);
    }

    static #RulesOf(type) {
        let rules = ItemLoot.#byType.get(type);
        if (!rules) ItemLoot.#byType.set(type, rules = []);
        return rules;
    }

    // As regras do tipo; o ModifyItemLoot do ModItem roda na primeira vez.
    static For(m) {
        const type = m.Type;
        if (!ItemLoot.#byType.has(type)) {
            const loot = new ItemLoot(type);
            Safe.Run(m.constructor.name + '.ModifyItemLoot', () => m.ModifyItemLoot(loot));
        }
        return ItemLoot.#RulesOf(type);
    }

    Add(rule) {
        this.rules.push(rule);
        return rule;
    }

    Remove(rule) {
        const i = this.rules.indexOf(rule);
        if (i >= 0) this.rules.splice(i, 1);
        return rule;
    }

    Get() {
        return [...this.rules];
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
