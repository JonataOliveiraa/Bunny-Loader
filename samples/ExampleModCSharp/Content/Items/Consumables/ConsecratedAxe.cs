using ExampleMod.Content.Invasions;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables
{
    public class ConsecratedAxe : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 20;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = 100000;
        }

        public override bool CanUseItem(Player player) => InvasionLoader.CanStart(ModContent.GetInstance<PaladinCrusade>());

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar, player.position);
                InvasionLoader.Start(ModContent.GetInstance<PaladinCrusade>());
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddIngredient(ItemID.SoulofLight, 3)
                .AddIngredient(ItemID.SoulofNight, 3)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
