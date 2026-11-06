using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ExampleBasicFish : ModItem {
        public override void SetStaticDefaults() {
            ItemID.Sets.CanBePlacedOnWeaponRacks[Type] = true;
            ItemID.Sets.IsBasicFish[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 34;
            Item.height = 34;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 5);
        }

        public override void AddRecipes() {
            Recipe.Create(ItemID.BlackenedFish)
                .AddIngredient(Type)
                .AddTile(TileID.Campfire)
                .Register();
            Recipe.Create(ItemID.CookedFish)
                .AddIngredient(Type)
                .AddTile(TileID.CookingPots)
                .Register();
        }
    }
}
