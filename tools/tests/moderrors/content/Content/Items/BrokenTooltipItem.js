// O caso do relato: um erro dentro do ModifyTooltips (nome não definido).
export class BrokenTooltipItem extends ModItem {
    SetDefaults() {
        this.Item.width = this.Item.height = 20;
    }

    ModifyTooltips(item, tooltips) {
        NAO_DEFINIDO;
        tooltips.push(new TooltipLine(this.Mod, 'Nunca', 'nunca chega aqui'));
    }
}
