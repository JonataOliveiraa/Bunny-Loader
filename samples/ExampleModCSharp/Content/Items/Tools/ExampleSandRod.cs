using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools
{
    public class ExampleSandRod : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;

            Item.channel = true;
            Item.knockBack = 5f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item8;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.rare = ItemRarityID.Blue;
            Item.noMelee = true;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override bool CanUseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }

            Point tilePos = Main.MouseWorld.ToTileCoordinates();
            Tile tile = Main.tile[tilePos];

            if (!tile.HasTile) {
                return false;
            }

            if (!Main.tileSand[tile.TileType]) {
                return false;
            }

            if (TileID.Sets.FallingBlockProjectile[tile.TileType] is not TileID.Sets.FallingBlockProjectileInfo data) {
                return false;
            }

            WorldGen.KillTile(tilePos.X, tilePos.Y, noItem: true);

            if (Main.tile[tilePos].HasTile) {
                return false;
            }

            Item.shoot = data.FallingProjectileType;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.TileManipulation, number: 4, number2: tilePos.X, number3: tilePos.Y);
            }

            return true;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position = Main.MouseWorld;
            player.LimitPointToPlayerReachableArea(ref position);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
