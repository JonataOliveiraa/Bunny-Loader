using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class CustomItemDrawingShowcase : ModItem
    {
        private static Asset<Texture2D> backTexture;
        private static Asset<Texture2D> frontTexture;

        private int drawMode = 0;
        private const int DrawModeGlowmask = 0;
        private const int DrawModePulse = 1;
        private const int DrawModeBehindTexture = 2;
        private const int DrawModeHighlightAfterImageEffect = 3;
        private const int DrawModeRockingRotation = 4;
        private const int Count = 5;

        public static LocalizedText RightClickText { get; private set; }
        public static LocalizedText[] DrawModeText { get; private set; }

        public override void Load() {
            backTexture = ModContent.Request<Texture2D>(Texture + "_Back");
            frontTexture = ModContent.Request<Texture2D>(Texture + "_Front");
        }

        public override void SetStaticDefaults() {
            RightClickText = this.GetLocalization("RightClick");
            DrawModeText = Enumerable.Range(0, 5).Select(i => this.GetLocalization($"DrawMode_{i}")).ToArray();
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;

            Item.holdStyle = ItemHoldStyleID.HoldFront;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useAnimation = 60;
            Item.useTime = 60;
        }

        public override bool CanRightClick() => true;

        public override bool ConsumeItem(Player player) => false;

        public override void RightClick(Player player) {
            drawMode = (drawMode + 1) % Count;
            Main.NewText(RightClickText.Format(drawMode, GetMessageForDrawMode()));
        }

        private string GetMessageForDrawMode() {
            if (DrawModeText.IndexInRange(drawMode)) {
                return DrawModeText[drawMode].Value;
            }

            return "Unknown mode";
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DrawModeDetails", $"drawMode #{drawMode}: {GetMessageForDrawMode()}"));
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (drawMode == DrawModePulse) {
                scale *= Main.essScale;
                drawColor *= Main.essScale;
                spriteBatch.Draw(TextureAssets.Item[Type].Value, position, frame, drawColor, 0, origin, scale, SpriteEffects.None, 0);
                return false;
            }
            else if (drawMode == DrawModeBehindTexture) {
                int backFrameNumber = (int)(Main.GameUpdateCount % 60 / 30);
                var backSourceRectangle = backTexture.Frame(verticalFrames: 2, frameY: backFrameNumber);
                var backOrigin = backSourceRectangle.Size() / 2;

                spriteBatch.Draw(backTexture.Value, position, backSourceRectangle, drawColor, 0, backOrigin, scale, SpriteEffects.None, 0);
            }
            else if (drawMode == DrawModeRockingRotation) {
                float rotation = MathF.Cos(Main.GameUpdateCount * 0.03f) * MathHelper.ToRadians(30);

                spriteBatch.Draw(TextureAssets.Item[Type].Value, position, frame, drawColor, rotation, origin, scale, SpriteEffects.None, 0);
                return false;
            }

            return true;
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (drawMode == DrawModeGlowmask) {
                spriteBatch.Draw(frontTexture.Value, position, frame, drawColor, 0, origin, scale, SpriteEffects.None, 0);
            }
        }

        public override bool PreDrawInWorld(WorldItem item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Main.GetItemDrawFrame(Item.type, out var itemTexture, out var itemFrame);
            Vector2 drawOrigin = itemFrame.Size() / 2f;

            Vector2 drawPosition = item.Bottom - Main.screenPosition - new Vector2(0, drawOrigin.Y);

            if (drawMode == DrawModePulse) {
                scale = scale * Main.essScale;
                lightColor = lightColor * Main.essScale;
                spriteBatch.Draw(itemTexture, drawPosition, itemFrame, lightColor, rotation, drawOrigin, scale, SpriteEffects.None, 0);
                return false;
            }
            else if (drawMode == DrawModeBehindTexture) {
                int backFrameNumber = (int)(Main.GameUpdateCount % 60 / 30);
                var backSourceRectangle = backTexture.Frame(verticalFrames: 2, frameY: backFrameNumber);
                var backOrigin = backSourceRectangle.Size() / 2f;

                spriteBatch.Draw(backTexture.Value, drawPosition, backSourceRectangle, lightColor, rotation, backOrigin, scale, SpriteEffects.None, 0);
            }
            else if (drawMode == DrawModeHighlightAfterImageEffect) {
                float counter = item.timeSinceItemSpawned / 240f + Main.GlobalTimeWrappedHourly * 0.04f;
                float offsetScale = Main.GlobalTimeWrappedHourly;
                offsetScale %= 4f;
                offsetScale /= 2f;
                if (offsetScale >= 1f) {
                    offsetScale = 2f - offsetScale;
                }

                offsetScale = offsetScale * 0.5f + 0.5f;

                for (float i = 0f; i < 1f; i += 0.25f) {
                    spriteBatch.Draw(itemTexture, drawPosition + new Vector2(0f, 8f).RotatedBy((i + counter) * ((float)Math.PI * 2f)) * offsetScale, itemFrame, new Color(90, 70, 255, 50), rotation, drawOrigin, scale, SpriteEffects.None, 0f);
                }

                for (float i = 0f; i < 1f; i += 0.34f) {
                    spriteBatch.Draw(itemTexture, drawPosition + new Vector2(0f, 4f).RotatedBy((i + counter) * ((float)Math.PI * 2f)) * offsetScale, itemFrame, new Color(140, 120, 255, 77), rotation, drawOrigin, scale, SpriteEffects.None, 0f);
                }
            }
            else if (drawMode == DrawModeRockingRotation) {
                rotation += MathF.Cos(Main.GameUpdateCount * 0.03f) * MathHelper.ToRadians(30);
            }

            return true;
        }

        public override void PostDrawInWorld(WorldItem item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Main.GetItemDrawFrame(Item.type, out var itemTexture, out var itemFrame);
            Vector2 origin = itemFrame.Size() / 2f;
            Vector2 drawPosition = item.Bottom - Main.screenPosition - new Vector2(0, origin.Y);

            if (drawMode == DrawModeGlowmask) {
                spriteBatch.Draw(frontTexture.Value, drawPosition, itemFrame, Color.White, rotation, origin, scale, SpriteEffects.None, 0);
            }
        }

        public override void HoldStyle(Player player, Rectangle heldItemFrame) {
            player.itemLocation.X = player.position.X + player.width * 0.5f + (heldItemFrame.Width * 0.5f - 10f) * player.direction;
        }

        public override bool ModifyItemDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData, ref DrawData? coloredDrawData, ref DrawData? glowmaskDrawData) {
            if (drawMode == DrawModeGlowmask) {
                glowmaskDrawData = drawData with { texture = frontTexture.Value };
            }
            else if (drawMode == DrawModePulse) {
                drawData = drawData with { scale = drawData.scale * Main.essScale, color = drawData.color * Main.essScale };
            }
            else if (drawMode == DrawModeBehindTexture) {
                int backFrameNumber = (int)(Main.GameUpdateCount % 60 / 30);
                var backSourceRectangle = backTexture.Frame(verticalFrames: 2, frameY: backFrameNumber);
                int originalHeight = drawData.sourceRect.Height > 0 ? drawData.sourceRect.Height : drawData.texture.Height;
                int adjustment = (backSourceRectangle.Height - originalHeight) / 2;
                var backOrigin = drawData.origin + new Vector2(adjustment);

                var behind = drawData with { texture = backTexture.Value, sourceRect = backSourceRectangle, origin = backOrigin };
                drawInfo.DrawDataCache.Add(behind);
            }
            else if (drawMode == DrawModeHighlightAfterImageEffect) {
                float counter = Main.GlobalTimeWrappedHourly * 0.29f;
                float offsetScale = Main.GlobalTimeWrappedHourly;
                offsetScale %= 4f;
                offsetScale /= 2f;
                if (offsetScale >= 1f) {
                    offsetScale = 2f - offsetScale;
                }

                offsetScale = offsetScale * 0.5f + 0.5f;

                for (float i = 0f; i < 1f; i += 0.25f) {
                    var farAfterimage = drawData with { position = drawData.position + new Vector2(0f, 8f).RotatedBy((i + counter) * ((float)Math.PI * 2f)) * offsetScale, color = new Color(90, 70, 255, 50) };
                    drawInfo.DrawDataCache.Add(farAfterimage);
                }

                for (float i = 0f; i < 1f; i += 0.34f) {
                    var farAfterimage = drawData with { position = drawData.position + new Vector2(0f, 4f).RotatedBy((i + counter) * ((float)Math.PI * 2f)) * offsetScale, color = new Color(140, 120, 255, 77) };
                    drawInfo.DrawDataCache.Add(farAfterimage);
                }
            }
            else if (drawMode == DrawModeRockingRotation) {
                drawData.rotation += MathF.Cos(Main.GameUpdateCount * 0.03f) * MathHelper.ToRadians(30);
            }

            return true;
        }
    }
}
