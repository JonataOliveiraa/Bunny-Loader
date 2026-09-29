// Arma corpo a corpo: tem de cair na camada da Espada de Madeira.
export class SortSword extends ModItem {
    SetDefaults(item) {
        item.damage = 10;
        item.melee = true;
        item.useStyle = Terraria.ID.ItemUseStyleID.Swing;
        item.useTime = item.useAnimation = 20;
        item.rare = 1;
    }
}
