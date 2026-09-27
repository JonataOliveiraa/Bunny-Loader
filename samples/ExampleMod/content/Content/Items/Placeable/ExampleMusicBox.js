const { ItemID } = Terraria.ID;

// Caixa de música: toca a música do chefe de exemplo quando ligada.
export class ExampleMusicBox extends ModItem {
    SetStaticDefaults() {
        ItemID.Sets.CanGetPrefixes[this.Type] = false;
        ItemID.Sets.ShimmerTransformToItem[this.Type] = ItemID.MusicBox;
        MusicLoader.AddMusicBox(this.Mod, MusicLoader.GetMusicSlot(this.Mod, 'Music/Ropocalypse2'),
                                this.Type, ModContent.TileType('ExampleMusicBoxTile'));
    }

    SetDefaults() {
        this.DefaultToMusicBox(ModContent.TileType('ExampleMusicBoxTile'), 0);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.MusicBox)
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
