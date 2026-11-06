using ExampleMod.Content.Invasions;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables
{
    public class GraveBell : ModItem
    {
        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 20;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = 20000;
        }

        public override bool CanUseItem(Player player) => InvasionLoader.CanStart(ModContent.GetInstance<GraveLegion>());

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar, player.position);
                InvasionLoader.Start(ModContent.GetInstance<GraveLegion>());
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Bone, 15)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
