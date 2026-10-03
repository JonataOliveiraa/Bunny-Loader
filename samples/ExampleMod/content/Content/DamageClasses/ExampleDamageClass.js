// Uma classe de dano de exemplo: herda os bônus de "todo dano" (Generic),
// conta como corpo a corpo e mágica para os efeitos do jogo, e tem 4% de
// crítico e 10 de penetração de armadura a mais.
export class ExampleDamageClass extends DamageClass {
    GetModifierInheritance(damageClass) {
        if (damageClass === DamageClass.Generic) return StatInheritanceData.Full;

        return new StatInheritanceData({
            damageInheritance: 0,
            critChanceInheritance: 0,
            attackSpeedInheritance: 0,
            armorPenInheritance: 0,
            knockbackInheritance: 0,
        });
    }

    GetEffectInheritance(damageClass) {
        return damageClass === DamageClass.Melee || damageClass === DamageClass.Magic;
    }

    SetDefaultStats(player) {
        player.GetCritChance(this).value += 4;
        player.GetArmorPenetration(this).value += 10;
    }

    get UseStandardCritCalcs() { return true; }

    ShowStatTooltipLine(player, lineName) {
        return lineName !== 'Speed';
    }
}
