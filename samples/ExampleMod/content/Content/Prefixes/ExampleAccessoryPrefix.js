// Um prefixo de acessório, como o do ExMod do TL: +4 de defesa.
export class ExampleAccessoryPrefix extends ModPrefix {
    get Category() { return PrefixCategory.Accessory; }

    // Acessório não tem dano nem velocidade para pesar no preço: o jogo usa
    // 1.05 por +1 de defesa (+4 = 1.2).
    ModifyValue(valueMult) {
        valueMult.value *= 1.2;
    }

    ApplyAccessoryEffects(player) {
        player.statDefense += 4;
    }

    // O jogo só escreve as linhas dos prefixos dele; "defesa" vem do texto dele.
    GetTooltipLines(item) {
        const line = new TooltipLine(this.Mod, 'PrefixAccDefense', '+4' + Terraria.Lang.tip[25].Value);
        line.IsModifier = true;
        return [line];
    }
}
