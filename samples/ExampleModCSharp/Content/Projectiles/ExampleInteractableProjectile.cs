using ExampleMod.Content.Items.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{

    public class ExampleInteractableProjectile : ModProjectile
    {

        private static Asset<Texture2D> highlightTexture;

        public override void Load() {
            highlightTexture = ModContent.Request<Texture2D>(Texture + "_Highlight");
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.InteractableProjectileItemIcons[Type] = ModContent.ItemType<ExampleInteractableProjectileItem>();
            Main.projFrames[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 24;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 10800;
            Projectile.drawLayer = ProjectileDrawLayerID.BehindProjectiles;

            DrawOriginOffsetY = -5;
        }

        public override void PostDraw(Player player, Color lightColor) {

            int highlightTextureDrawMode = TryInteracting();
            if (highlightTextureDrawMode == 0) {

                return;
            }

            int lightValue = (lightColor.R + lightColor.G + lightColor.B) / 3;
            if (lightValue > 10) {
                bool isProjectileSelected = highlightTextureDrawMode == 2;
                Color selectionGlowColor = Colors.GetSelectionGlowColor(isProjectileSelected, lightValue);

                var drawData = Projectile.GetDefaultDrawData(player, lightColor);

                Main.EntitySpriteDraw(drawData with {
                    texture = highlightTexture.Value,
                    color = selectionGlowColor
                });
            }
        }

        private int TryInteracting() {
            if (Main.gamePaused || Main.gameMenu) {
                return 0;
            }

            bool cursorHighlights = Main.SmartCursorIsUsed || PlayerInput.UsingGamepad;
            Player localPlayer = Main.LocalPlayer;
            Vector2 compareSpot = localPlayer.Center;
            if (!localPlayer.IsProjectileInteractableAndInInteractionRange(Projectile, compareSpot)) {
                return 0;
            }

            Vector2 realMouseWorld = Main.ReverseGravitySupport(Main.MouseScreen) + Main.screenPosition;
            bool mouseDirectlyOver = Projectile.Hitbox.Contains(realMouseWorld.ToPoint());
            bool interactingWithThisProjectile = mouseDirectlyOver || Main.SmartInteractProj == Projectile.whoAmI;
            if (!interactingWithThisProjectile || localPlayer.lastMouseInterface) {
                if (cursorHighlights) {
                    return 1;
                }
                else {
                    return 0;
                }
            }

            Main.HasInteractableObjectThatIsNotATile = true;
            if (mouseDirectlyOver) {
                localPlayer.noThrow = 2;

                localPlayer.cursorItemIconEnabled = true;
                localPlayer.cursorItemIconID = ModContent.ItemType<ExampleInteractableProjectileItem>();
            }

            if (PlayerInput.UsingGamepad) {
                localPlayer.GamepadEnableGrappleCooldown();
            }
            if (Main.mouseRight && Main.mouseRightRelease && Player.BlockInteractionWithProjectiles == 0) {
                Main.mouseRightRelease = false;
                localPlayer.tileInteractAttempted = true;
                localPlayer.tileInteractionHappened = true;
                localPlayer.releaseUseTile = false;

                SoundEngine.PlaySound(Main.rand.NextBool(10) ? (SoundStyle)SoundID.Duck : SoundID.Item59);
            }

            if (cursorHighlights) {
                return 2;
            }
            else {
                return 0;
            }
        }

        public override void AI() {

            Projectile.frameCounter++;
            Projectile.frame = Projectile.frameCounter / 4;
            if (Projectile.frame >= 5) {
                Projectile.frame = 8 - Projectile.frame;
            }
            if (Projectile.frameCounter >= 8 * 4) {
                Projectile.frameCounter = 0;
            }

            Main.CurrentFrameFlags.HadAnActiveInteractibleProjectile = true;

            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 1000; i++) {
                    Projectile otherProjectile = Main.projectile[i];
                    if (i != Projectile.whoAmI && otherProjectile.active && otherProjectile.owner == Projectile.owner && otherProjectile.type == Projectile.type) {
                        if (Projectile.timeLeft >= otherProjectile.timeLeft) {
                            otherProjectile.Kill();
                        }
                        else {
                            Projectile.Kill();
                        }
                    }
                }
            }

            if (Projectile.ai[0] == 0f) {
                if (Projectile.velocity.Length() < 0.1f) {

                    Projectile.velocity.X = 0f;
                    Projectile.velocity.Y = 0f;
                    Projectile.ai[0] = 1f;
                    Projectile.ai[1] = 45f;
                    return;
                }

                Projectile.velocity *= 0.94f;
                Projectile.direction = Projectile.velocity.X < 0f ? -1 : 1;
                Projectile.spriteDirection = Projectile.direction;
                return;
            }

            Projectile.direction = Main.player[Projectile.owner].Center.X < Projectile.Center.X ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            Projectile.ai[1] += 1f;
            float acceleration = 0.005f;
            if (Projectile.ai[1] > 0f) {
                Projectile.velocity.Y -= acceleration;
            }
            else {
                Projectile.velocity.Y += acceleration;
            }
            if (Projectile.ai[1] >= 90f) {
                Projectile.ai[1] *= -1f;
            }
        }
    }
}
