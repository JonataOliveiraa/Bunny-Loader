using ExampleMod.Content.Tiles.Furniture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories
{

    public class ExampleMultiExtraJumpAccessory : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToAccessory(20, 26);
            Item.SetShopValues(ItemRarityColor.Orange3, Item.buyPrice(gold: 2));
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetJumpState<MultipleUseExtraJump>().Enable();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>(35)
                .AddTile<ExampleWorkbench>()
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {

            int index = tooltips.FindIndex(static line => line.Text.Contains("<JUMPS>"));
            if (index >= 0) {

                ref string text = ref tooltips[index].Text;
                text = text.Replace("<JUMPS>", $"{Main.LocalPlayer.GetModPlayer<MultipleUseExtraJumpPlayer>().jumpsRemaining}");
            }
        }
    }

    public class MultipleUseExtraJump : ExtraJump
    {
        public override Position GetDefaultPosition() => new After(BlizzardInABottle);

        public override float GetDurationMultiplier(Player player) {

            return player.GetModPlayer<MultipleUseExtraJumpPlayer>().jumpsRemaining switch {
                1 => 0.2f,
                2 => 0.5f,
                3 => 0.8f,
                _ => 0f
            };
        }

        public override void OnRefreshed(Player player) {

            player.GetModPlayer<MultipleUseExtraJumpPlayer>().jumpsRemaining = 3;
        }

        public override void OnStarted(Player player, ref bool playSound) {

            ref int jumps = ref player.GetModPlayer<MultipleUseExtraJumpPlayer>().jumpsRemaining;

            int offsetY = player.height;
            if (player.gravDir == -1f)
                offsetY = 0;

            offsetY -= 16;

            Vector2 center = player.Top + new Vector2(0, offsetY);

            if (jumps == 3) {
                const int numDusts = 40;
                for (int i = 0; i < numDusts; i++) {
                    (float sin, float cos) = MathF.SinCos(MathHelper.ToRadians(i * 360 / numDusts));

                    float amplitudeX = cos * (player.width + 10) / 2f;
                    float amplitudeY = sin * 6;

                    Dust dust = Dust.NewDustPerfect(center + new Vector2(amplitudeX, amplitudeY), DustID.BlueFlare, -player.velocity * 0.5f, Scale: 1f);
                    dust.noGravity = true;
                }
            }
            else if (jumps == 2) {
                const int numDusts = 30;
                for (int i = 0; i < numDusts; i++) {
                    (float sin, float cos) = MathF.SinCos(MathHelper.ToRadians(i * 360 / numDusts));

                    float amplitudeX = cos * (player.width + 2) / 2f;
                    float amplitudeY = sin * 5;

                    Dust dust = Dust.NewDustPerfect(center + new Vector2(amplitudeX, amplitudeY), DustID.BlueFlare, -player.velocity * 0.35f, Scale: 1f);
                    dust.noGravity = true;
                }
            }
            else {
                const int numDusts = 24;
                for (int i = 0; i < numDusts; i++) {
                    (float sin, float cos) = MathF.SinCos(MathHelper.ToRadians(i * 360 / numDusts));

                    float amplitudeX = cos * (player.width - 4) / 2f;
                    float amplitudeY = sin * 3;

                    Dust dust = Dust.NewDustPerfect(center + new Vector2(amplitudeX, amplitudeY), DustID.BlueFlare, -player.velocity * 0.2f, Scale: 1f);
                    dust.noGravity = true;
                }
            }

            playSound = false;

            float pitch = jumps switch {
                1 => 0.5f,
                2 => 0.1f,
                3 => -0.2f,
                _ => 0
            };

            SoundEngine.PlaySound(((SoundStyle)SoundID.Item8) with { Pitch = pitch, PitchVariance = 0.04f }, player.Bottom);

            jumps--;

            if (jumps > 0)
                player.GetJumpState(this).Available = true;
        }
    }

    public class MultipleUseExtraJumpPlayer : ModPlayer
    {
        public int jumpsRemaining;
    }
}
