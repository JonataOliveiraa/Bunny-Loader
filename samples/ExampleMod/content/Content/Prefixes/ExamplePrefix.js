// Um prefixo de arma, como o ExamplePrefix do tModLoader. O ExampleDerivedPrefix
// herda deste e só muda o Power.
export class ExamplePrefix extends ModPrefix {
    // Uma propriedade nossa, que a classe derivada sobrescreve.
    get Power() { return 1; }

    // Que itens podem ganhar o prefixo (o padrão é Custom).
    get Category() { return PrefixCategory.AnyWeapon; }

    // O peso na rolagem, perto dos do jogo (1 cada). Custom: use o ChoosePrefix do item.
    RollChance(item) {
        return 5;
    }

    CanRoll(item) {
        return true;
    }

    // Dano, repulsão, tempo de uso, tamanho, velocidade do tiro, mana e crítico.
    // Os `ref` chegam como Ref: mexa no .value.
    SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus) {
        damageMult.value *= 1 + 0.20 * this.Power;
    }

    // O preço do item com este prefixo.
    ModifyValue(valueMult) {
        valueMult.value *= 1 + 0.05 * this.Power;
    }

    // O resto que o prefixo muda no item.
    Apply(item) {
    }

    // O "+20% de dano" o jogo já escreve; estas são linhas a mais.
    GetTooltipLines(item) {
        const power = this.Power;
        const powerLine = new TooltipLine(this.Mod, 'PrefixWeaponAwesome',
            ModLocalization.Translate('Prefixes.PowerTooltip').replace('{0}', (power >= 0 ? '+' : '') + power));
        powerLine.IsModifier = true;

        const description = new TooltipLine(this.Mod, 'PrefixWeaponAwesomeDescription',
            ModLocalization.Translate('Prefixes.' + this.Name + '.AdditionalTooltip'));
        description.IsModifier = true;

        return [powerLine, description];
    }
}
