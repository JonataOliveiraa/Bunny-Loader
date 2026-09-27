const { BuffID } = Terraria.ID;

export class ExampleBreastplate extends ModItem {
    static MaxManaIncrease = 20;
    static MaxMinionIncrease = 1;

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i]
                .replace('{0}', ExampleBreastplate.MaxManaIncrease)
                .replace('{1}', ExampleBreastplate.MaxMinionIncrease);
        }
    }

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 18;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.defense = 6;
    }

    UpdateEquip(item, player) {
        player.buffImmune[BuffID.OnFire] = true;
        player.statManaMax2 += ExampleBreastplate.MaxManaIncrease;
        player.maxMinions += ExampleBreastplate.MaxMinionIncrease;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
