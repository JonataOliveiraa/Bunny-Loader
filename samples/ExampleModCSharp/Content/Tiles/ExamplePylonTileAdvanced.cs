using ExampleMod.Content.Items.Placeable;
using ExampleMod.Content.TileEntities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles
{
    public class ExamplePylonTileAdvanced : ModPylon
    {
        public const int CrystalVerticalFrameCount = 8;

        public Asset<Texture2D> crystalTexture;
        public Asset<Texture2D> crystalHighlightTexture;
        public Asset<Texture2D> mapIcon;

        public override void Load() {
            crystalTexture = ModContent.Request<Texture2D>(Texture.Replace("Advanced", "") + "_Crystal");
            crystalHighlightTexture = ModContent.Request<Texture2D>(Texture.Replace("Advanced", "") + "_CrystalHighlight");
            mapIcon = ModContent.Request<Texture2D>(Texture.Replace("Advanced", "") + "_MapIcon");
        }

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(0, 2);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.StyleHorizontal = true;

            AdvancedPylonTileEntity advancedEntity = ModContent.GetInstance<AdvancedPylonTileEntity>();
            TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(advancedEntity.PlacementPreviewHook_CheckIfCanPlace, 1, 0, true);
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(advancedEntity.Hook_AfterPlacement, -1, 0, false);

            TileObjectData.addTile(Type);

            TileID.Sets.InteractableByNPCs[Type] = true;

            AddToArray(ref TileID.Sets.CountsAsPylon);

            LocalizedText pylonName = CreateMapEntryName();
            AddMapEntry(Color.Black, pylonName);
        }

        public override NPCShop.Entry GetNPCShopEntry() {
            return new NPCShop.Entry(ModContent.ItemType<ExamplePylonItemAdvanced>());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {
            return true;
        }

        public override bool RightClick(int i, int j) {
            Main.mapFullscreen = true;
            SoundEngine.PlaySound(SoundID.MenuOpen);
            return true;
        }

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.cursorItemIconEnabled = true;
            Main.LocalPlayer.cursorItemIconID = ModContent.ItemType<ExamplePylonItemAdvanced>();
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            ModContent.GetInstance<AdvancedPylonTileEntity>().Kill(i, j);
        }

        public override bool ValidTeleportCheck_NPCCount(TeleportPylonInfo pylonInfo, int defaultNecessaryNPCCount) {
            return true;
        }

        public override void ValidTeleportCheck_DestinationPostCheck(TeleportPylonInfo destinationPylonInfo, ref bool destinationPylonValid, ref string errorKey) {
            if (TileEntity.TryGet(destinationPylonInfo.PositionInTiles, out AdvancedPylonTileEntity destination) && !destination.isActive) {
                destinationPylonValid = false;
                errorKey = "Mods.ExampleMod.MessageInfo.UnstablePylonIsOff";
            }
        }

        public override void ValidTeleportCheck_NearbyPostCheck(TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool anyNearbyValidPylon, ref string errorKey) {
            if (TileEntity.TryGet(nearbyPylonInfo.PositionInTiles, out AdvancedPylonTileEntity nearby) && !nearby.isActive) {
                destinationPylonValid = false;
                errorKey = "Mods.ExampleMod.MessageInfo.NearbyUnstablePylonIsOff";
            }
        }

        public override void ModifyTeleportationPosition(TeleportPylonInfo destinationPylonInfo, ref Vector2 teleportationPosition) {
            teleportationPosition = destinationPylonInfo.PositionInTiles.ToWorldCoordinates(8f, -32f);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = Main.DiscoColor.R / 255f * 0.75f;
            g = Main.DiscoColor.G / 255f * 0.75f;
            b = Main.DiscoColor.B / 255f * 0.75f;
        }

        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) {
            if (drawData.tileFrameX % 36 == 0 && drawData.tileFrameY == 0 && TileEntity.TryGet(new Point16(i, j), out AdvancedPylonTileEntity entity) && entity.isActive) {
                Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
            }
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            DefaultDrawPylonCrystal(spriteBatch, i, j, crystalTexture, crystalHighlightTexture, new Vector2(0f, -18f), Main.DiscoColor * 0.1f, Main.DiscoColor, 1, CrystalVerticalFrameCount);
        }

        public override void DrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, TeleportPylonInfo pylonInfo, bool isNearPylon, Color drawColor, float deselectedScale, float selectedScale) {
            if (!TileEntity.TryGet(pylonInfo.PositionInTiles, out AdvancedPylonTileEntity entity)) {
                return;
            }

            drawColor = !entity.isActive ? Color.Gray * 0.5f : drawColor;
            bool mouseOver = DefaultDrawMapIcon(ref context, mapIcon, pylonInfo.PositionInTiles.ToVector2() + new Vector2(1, 1.5f), drawColor, deselectedScale, selectedScale);
            DefaultMapClickHandle(mouseOver, pylonInfo, ModContent.GetInstance<ExamplePylonItemAdvanced>().DisplayName.Key, ref mouseOverText);
        }
    }
}
