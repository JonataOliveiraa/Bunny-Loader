// Uma classe de dano, como o DamageClass do tModLoader: decide de quais
// outras herda bônus (dano, crítico, velocidade, penetração, repulsão), como
// quais "conta" para os efeitos do jogo (Pedra de Magma, Spectre) e quais
// prefixos o item aceita. As do jogo são estáticas (DamageClass.Melee...);
// as de mod são registradas sozinhas e pegas com ModContent.GetInstance(Classe).
//
// No item: `this.Item.DamageType = DamageClass.Melee`. No jogador:
// `player.GetDamage(DamageClass.Generic).Additive += 0.1`.
class DamageClass {
    static get Default() { return DamageClassLoader.Vanilla.Default; }
    static get Generic() { return DamageClassLoader.Vanilla.Generic; }
    static get Melee() { return DamageClassLoader.Vanilla.Melee; }
    static get MeleeNoSpeed() { return DamageClassLoader.Vanilla.MeleeNoSpeed; }
    static get Ranged() { return DamageClassLoader.Vanilla.Ranged; }
    static get Magic() { return DamageClassLoader.Vanilla.Magic; }
    static get Summon() { return DamageClassLoader.Vanilla.Summon; }
    static get SummonMeleeSpeed() { return DamageClassLoader.Vanilla.SummonMeleeSpeed; }
    static get MagicSummonHybrid() { return DamageClassLoader.Vanilla.MagicSummonHybrid; }
    static get Throwing() { return DamageClassLoader.Vanilla.Throwing; }

    GetModifierInheritance(damageClass) {
        return damageClass === DamageClass.Generic ? StatInheritanceData.Full : StatInheritanceData.None;
    }

    GetEffectInheritance(damageClass) { return false; }

    GetPrefixInheritance(damageClass) { return this.GetEffectInheritance(damageClass); }

    SetStaticDefaults() {}

    SetDefaultStats(player) {}

    get UseStandardCritCalcs() { return true; }

    // lineName: 'Damage', 'CritChance', 'Speed' ou 'Knockback'.
    ShowStatTooltipLine(player, lineName) { return true; }

    CountsAsClass(damageClass) {
        return DamageClassLoader.CountsAs(this, DamageClassLoader.Resolve(damageClass));
    }

    GetsPrefixesFor(damageClass) {
        const other = DamageClassLoader.Resolve(damageClass);
        return this === other || !!Safe.Run(this.constructor.name + '.GetPrefixInheritance', () => this.GetPrefixInheritance(other));
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof DamageClass)) {
            throw new TypeError('DamageClass.register(Classe): passe a classe, que estende DamageClass');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        DamageClassLoader.Add(inst);
        return inst.Type;
    }
}
