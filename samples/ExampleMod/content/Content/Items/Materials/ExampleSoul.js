const { ItemID } = Terraria.ID;
const { Main } = Terraria;
const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];

export class ExampleSoul extends ModItem {
    SetStaticDefaults() {
        this.SetItemAnimation(4, 6);

        ItemID.Sets.AnimatesAsSoul[this.Type] = true;
        ItemID.Sets.ItemIconPulse[this.Type] = true;
        ItemID.Sets.ItemNoGravity[this.Type] = true;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.SoulofSight);
        this.Item.rare = ItemRarityID.Pink;
        this.Item.value = Terraria.Item.buyPrice(0, 1, 0, 0);
        this.Item.maxStack = ModItem.CommonMaxStack;
    }

    PostUpdate(item) {
        const glow = 0.45 * Main.essScale;
        AddLight(item.Center, 0.53 * glow, 0.81 * glow, 0.92 * glow);
    }

    GetAlpha(item, lightColor) {
        return Color.new(255, 255, 255, 50);
    }
}
