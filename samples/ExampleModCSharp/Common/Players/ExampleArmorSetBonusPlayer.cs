using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class ExampleArmorSetBonusPlayer : ModPlayer
    {
        public bool ExampleSetHood;
        public int ShadowStyle = 0;
        public bool CustomShadow;
        private int CustomShadowTimer;
        private Color CustomShadowColor = Color.White;

        public override void ResetEffects() {
            ExampleSetHood = false;
            CustomShadow = false;
        }

        public override void ArmorSetBonusActivated() {
            if (!ExampleSetHood) {
                return;
            }

            if (ShadowStyle == 4) {
                ShadowStyle = 0;
            }
            ShadowStyle = (ShadowStyle + 1) % 4;
            ShowMessageForShadowStyle();
        }

        public override void ArmorSetBonusHeld(int holdTime) {
            if (!ExampleSetHood) {
                return;
            }

            if (holdTime == 60) {
                ShadowStyle = ShadowStyle == 4 ? 0 : 4;
                ShowMessageForShadowStyle();
            }
        }

        private void ShowMessageForShadowStyle() {
            string styleName = ShadowStyle switch {
                1 => "armorEffectDrawShadow",
                2 => "armorEffectDrawOutlines",
                3 => "armorEffectDrawOutlinesForbidden",
                4 => "CustomShadow",
                _ => "None",
            };
            Main.NewText("Current shadow style: " + styleName);
        }

        public override void DrawPlayer(Camera camera) {
            if (!CustomShadow) {
                return;
            }

            CustomShadowTimer++;
            int clones = 3;
            if (Player.statLife < Player.statLifeMax2 / 2) {
                clones += 1;
            }
            Vector2 playerPosition = Player.position + new Vector2(0f, Player.gfxOffY);

            for (int i = 0; i < clones; i++) {

                float rotation = CustomShadowTimer * 0.03f + i * (MathHelper.TwoPi / clones);
                float distanceFromPlayer = MathF.Sin(CustomShadowTimer * 0.06f);
                Vector2 drawPosition = playerPosition + new Vector2(0f, distanceFromPlayer).RotatedBy(rotation) * 25;
                float shadow = 1 - Utils.Remap(Math.Abs(distanceFromPlayer), 0, 1, 0.1f, 0.4f, clamped: true);

                CustomShadowColor = Color.Lerp(Color.Aqua, Color.Red, (float)i / (clones - 1));

                Main.PlayerRenderer.DrawPlayer(camera, Player, drawPosition, Player.fullRotation, Player.fullRotationOrigin, shadow, 1f, default);
            }
            CustomShadowColor = Color.White;

        }

        public override void TransformDrawData(ref PlayerDrawSet drawInfo) {

            if (CustomShadowColor.PackedValue == Color.White.PackedValue) {
                return;
            }

            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++) {
                DrawData value = drawInfo.DrawDataCache[i];

                value.color = value.color.MultiplyRGBA(CustomShadowColor);
                drawInfo.DrawDataCache[i] = value;
            }
        }
    }
}
