using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools
{

    internal class ExampleMagicMirror : ExampleItem
    {
        private static readonly Color[] itemNameCycleColors = [
            new Color(254, 105, 47),
            new Color(190, 30, 209),
            new Color(34, 221, 151),
            new Color(0, 106, 185),
        ];

        public override string Texture => $"Terraria/Images/Item_{ItemID.IceMirror}";

        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.IceMirror);
            Item.color = Color.Violet;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) {

            if (Main.rand.NextBool()) {
                Dust.NewDust(player.position, player.width, player.height, DustID.MagicMirror, 0f, 0f, 150, Color.White, 1.1f);
            }

            if (player.itemTime == 0) {
                player.ApplyItemTime(Item);
            }
            else if (player.itemTime == player.itemTimeMax / 2) {

                for (int d = 0; d < 70; d++) {
                    Dust.NewDust(player.position, player.width, player.height, DustID.MagicMirror, player.velocity.X * 0.5f, player.velocity.Y * 0.5f, 150, default, 1.5f);
                }

                player.RemoveAllGrapplingHooks();

                player.Spawn(PlayerSpawnContext.RecallFromItem);

                for (int d = 0; d < 70; d++) {
                    Dust.NewDust(player.position, player.width, player.height, DustID.MagicMirror, 0f, 0f, 150, default, 1.5f);
                }
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {

            int numColors = itemNameCycleColors.Length;

            foreach (TooltipLine line2 in tooltips) {
                if (line2.Mod == "Terraria" && line2.Name == "ItemName") {
                    float fade = (Main.GameUpdateCount % 60) / 60f;
                    int index = (int)((Main.GameUpdateCount / 60) % numColors);
                    int nextIndex = (index + 1) % numColors;

                    line2.Color = Color.Lerp(itemNameCycleColors[index], itemNameCycleColors[nextIndex], fade);
                }
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
