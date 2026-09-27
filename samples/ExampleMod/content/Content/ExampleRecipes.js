const { ItemID, TileID } = Terraria.ID;

export class ExampleRecipes extends ModSystem {
    static ExampleGroup = null;

    AddRecipeGroups() {
        ExampleRecipes.ExampleGroup = ModRecipe.CreateRecipeGroup('ExampleItem', [
            ModItem.getTypeByName('ExampleItem'),
            ModItem.getTypeByName('ExampleSoul'),
        ]);
    }

    AddRecipes() {
        new ModRecipe()
            .SetResult(ItemID.RodofDiscord)
            .AddIngredient(ItemID.ChaosFish, 10)
            .AddIngredient(ItemID.HallowedBar, 20)
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();

        new ModRecipe()
            .SetResult(ItemID.LifeCrystal)
            .AddIngredient(ItemID.LesserHealingPotion, 50)
            .AddTile(TileID.WorkBenches)
            .Register();

        new ModRecipe()
            .SetResult(ItemID.SpikyBall, 50)
            .AddRecipeGroup('IronBar')
            .AddTile(TileID.Anvils)
            .SetProperty('needSnowBiome', true)
            .Register();

        new ModRecipe()
            .SetResult(ModItem.getTypeByName('ExampleMeleeWeapon'))
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 50)
            .AddRecipeGroup(ExampleRecipes.ExampleGroup)
            .Register();
    }
}
