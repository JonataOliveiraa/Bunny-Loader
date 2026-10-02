// A base do exemplo da documentação (04-conteudo-novo.md, Classes base).
export class BardItem extends ModItem {
    static Autoload = false;

    InspirationCost = 1;

    SetDefaults() {
        this.Item.useStyle = 5;
        this.Item.useTime = this.Item.useAnimation = 20;
        this.Item.noMelee = true;
        this.Item.rare = ItemRarityID.Green;
    }

    ModifyTooltips(item, tooltips) {
        tooltips.push(new TooltipLine(this.Mod, 'Inspiration', 'Custa ' + this.InspirationCost + ' de inspiração'));
    }
}
