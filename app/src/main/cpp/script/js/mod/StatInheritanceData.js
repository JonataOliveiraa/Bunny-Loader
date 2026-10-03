// Quanto uma classe de dano herda dos bônus de outra (o StatInheritanceData do
// tModLoader): 1 = tudo, 0 = nada, como uma porcentagem. Os argumentos com
// nome do C# viram um objeto: new StatInheritanceData({ damageInheritance: 1 }).
class StatInheritanceData {
    constructor(damageInheritance = 0, critChanceInheritance = 0, attackSpeedInheritance = 0,
                armorPenInheritance = 0, knockbackInheritance = 0) {
        if (damageInheritance !== null && typeof damageInheritance === 'object') {
            const o = damageInheritance;
            damageInheritance = o.damageInheritance || 0;
            critChanceInheritance = o.critChanceInheritance || 0;
            attackSpeedInheritance = o.attackSpeedInheritance || 0;
            armorPenInheritance = o.armorPenInheritance || 0;
            knockbackInheritance = o.knockbackInheritance || 0;
        }
        this.damageInheritance = damageInheritance;
        this.critChanceInheritance = critChanceInheritance;
        this.attackSpeedInheritance = attackSpeedInheritance;
        this.armorPenInheritance = armorPenInheritance;
        this.knockbackInheritance = knockbackInheritance;
    }

    // Um novo a cada leitura: o `Full with { attackSpeedInheritance = 0 }` do
    // C# vira Object.assign(StatInheritanceData.Full, { attackSpeedInheritance: 0 }).
    static get Full() { return new StatInheritanceData(1, 1, 1, 1, 1); }
    static get None() { return new StatInheritanceData(); }

    get IsNone() {
        return !this.damageInheritance && !this.critChanceInheritance && !this.attackSpeedInheritance &&
               !this.armorPenInheritance && !this.knockbackInheritance;
    }
}
