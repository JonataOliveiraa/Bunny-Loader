const { ItemID, TileID } = Terraria.ID;

// Bloco de fogo vivo de exemplo (a luz dele: LivingFireLight, no tile).
export class ExampleLivingFire extends ModItem {
    SetStaticDefaults() {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[this.Type] = true;
    }

    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleLivingFireTile'));
        this.Item.width = 12;
        this.Item.height = 12;
    }

    AddRecipes() {
        this.CreateRecipe(20)
            .AddIngredient(ItemID.LivingFireBlock, 20)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(TileID.CrystalBall)
            .Register();
    }
}
