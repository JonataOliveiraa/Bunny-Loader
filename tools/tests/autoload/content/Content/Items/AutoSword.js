// Textura: Assets/Textures/Items/AutoSword.png, o espelho deste arquivo.
export class AutoSword extends ModItem {
    SetDefaults(item) {
        item.damage = 12;
        item.useStyle = Terraria.ID.ItemUseStyleID.Swing;
        item.useTime = item.useAnimation = 20;
    }
}
