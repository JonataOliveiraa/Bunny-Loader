const { SoundID, ItemUseStyleID } = Terraria.ID;

// Uma arma de longo alcance que pega os prefixos de corpo a corpo e de magia, e
// não os de longo alcance, como a ExampleMultiplePrefixCategoryWeapon do tModLoader.
export class ExampleMultiplePrefixCategoryWeapon extends ModItem {
    SetDefaults() {
        const item = this.Item;
        item.ranged = true;
        item.width = 40;
        item.height = 40;
        item.useStyle = ItemUseStyleID.Swing;
        item.useTime = 30;
        item.useAnimation = 30;
        item.autoReuse = true;
        item.damage = 70;
        item.knockBack = 4;
        item.crit = 6;
        item.mana = 6;   // gasta mana, então os prefixos de magia pegam todos
        item.value = Terraria.Item.buyPrice(0, 1, 0, 0);
        item.rare = ItemRarityID.Green;
        item.UseSound = SoundID.Item1;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 20)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }

    // As categorias de prefixo, contra o que o dano de longo alcance daria.
    MeleePrefix(item) {
        return true;
    }

    MagicPrefix(item) {
        return true;
    }

    RangedPrefix(item) {
        return false;
    }
}
