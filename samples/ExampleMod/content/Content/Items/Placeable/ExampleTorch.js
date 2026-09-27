const { ItemID } = Terraria.ID;
const Main = Terraria.Main;

// Tocha de exemplo: a tocha do jogo, colocando a de exemplo. Na mão e no chão, ilumina.
export class ExampleTorch extends ModItem {
    SetStaticDefaults() {
        ItemID.Sets.ShimmerTransformToItem[this.Type] = ItemID.ShimmerTorch;
        ItemID.Sets.SingleUseInGamepad[this.Type] = true;
        ItemID.Sets.Torches[this.Type] = true;
    }

    SetDefaults() {
        this.DefaultToTorch(ModContent.TileType('ExampleTorch'), 0, false);
        this.Item.value = 50;
    }

    HoldItem(item, player) {
        if (player.wet) return;

        const at = Vector2.new(player.itemLocation.X + 12 * player.direction + player.velocity.X,
                               player.itemLocation.Y - 14 + player.velocity.Y);
        Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'](player.RotatedRelativePoint(at, false, true), 1, 1, 1);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
