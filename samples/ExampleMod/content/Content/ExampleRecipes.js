const { ItemID, TileID, ProjectileID } = Terraria.ID;
const Main = Terraria.Main;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

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

        // Condições (as do tModLoader): no deserto, perto de água, à noite e com
        // menos da metade da vida. Fora delas, a receita some do menu de criação.
        new ModRecipe()
            .SetResult(ModItem.getTypeByName('ExampleItem'), 100)
            .AddIngredient(ItemID.StoneBlock)
            .AddIngredient(ItemID.Acorn, 10)
            .AddTile(TileID.WorkBenches)
            .AddCondition(Condition.InDesert)
            .AddCondition(Condition.NearWater)
            .AddCondition(Condition.TimeNight)
            .AddCondition('Conditions.LowHealth', () => {
                const player = Main.player[Main.myPlayer];
                return player.statLife < player.statLifeMax / 2;
            })
            .Register();

        // A Corrente é pedida mas não é gasta, como a Mesa de Alquimia faz com as poções.
        new ModRecipe()
            .SetResult(ItemID.AlphabetStatueJ)
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Chain)
            .AddConsumeIngredientCallback(ExampleRecipes.DontConsumeChain)
            .AddTile(TileID.HeavyWorkBench)
            .Register();

        // Código depois de criar: às vezes, fogos de artifício.
        new ModRecipe()
            .SetResult(ItemID.AlphabetStatueZ)
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Chain)
            .AddOnCraftCallback(ExampleRecipes.RandomlySpawnFireworks)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
    }

    // amount é Ref: 0 = o ingrediente não sai.
    static DontConsumeChain(recipe, type, amount, isDecrafting) {
        if (type === ItemID.Chain) amount.value = 0;
    }

    static RandomlySpawnFireworks(recipe, item, consumedItems, destinationStack) {
        if (!Rand.NextBool(3)) return;

        const player = Main.player[Main.myPlayer];
        const firework = ProjectileID.RocketFireworksBoxRed + Rand.Next(4);
        const velocity = Vector2.RotatedByRandom(Vector2.new(0, -Rand.NextFloat(2, 4)), 0.3);
        NewProjectile(player.GetProjectileSource_Item(item), player.Top, velocity, firework, 0, 0, Main.myPlayer, 0, 0, 0, null);
        player['void QuickSpawnItem(IEntitySource source, int item, int stack)'](player.GetItemSource_Misc(0), ItemID.Confetti, 5);
    }
}
