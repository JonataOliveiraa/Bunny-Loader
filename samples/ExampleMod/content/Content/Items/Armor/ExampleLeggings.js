export class ExampleLeggings extends ModItem {
    static MoveSpeedBonus = 5;

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', ExampleLeggings.MoveSpeedBonus);
        }
    }

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 18;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.defense = 5;
    }

    UpdateEquip(item, player) {
        player.moveSpeed += ExampleLeggings.MoveSpeedBonus / 100;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
