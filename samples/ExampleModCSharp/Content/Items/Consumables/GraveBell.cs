using ExampleMod.Content.Invasions;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables
{
    // This item starts the Grave Legion invasion, the same way the Goblin Battle Standard starts the Goblin Army.
    public class GraveBell : ModItem
    {
        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 20;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true; // Used up when the invasion starts.
            Item.rare = ItemRarityID.Orange;
            Item.value = 20000;
        }

        // InvasionLoader.CanStart checks that no other invasion (modded or vanilla) is active, then calls GraveLegion.CanStart.
        // Returning false here means the item isn't used up when the invasion can't start.
        public override bool CanUseItem(Player player) => InvasionLoader.CanStart(ModContent.GetInstance<GraveLegion>());

        public override bool? UseItem(Player player) {
            // Only the player using the item starts the invasion. In multiplayer, InvasionLoader.Start sends the request to the server.
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
