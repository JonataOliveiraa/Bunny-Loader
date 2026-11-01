using ExampleMod.Common;
using ExampleMod.Common.Systems;
using ExampleMod.Content.Items.Placeable;
using ExampleMod.Content.TileEntities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class ExamplePylonTile : ModPylon
{
    public const int CrystalVerticalFrameCount = 8;

    public Asset<Texture2D> crystalTexture;
    public Asset<Texture2D> crystalHighlightTexture;
    public Asset<Texture2D> mapIcon;

    public override void Load()
    {
        crystalTexture = ModContent.Request<Texture2D>(Texture + "_Crystal");
        crystalHighlightTexture = ModContent.Request<Texture2D>(Texture + "_CrystalHighlight");
        mapIcon = ModContent.Request<Texture2D>(Texture + "_MapIcon");
    }

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;
        Main.tileFrameImportant[Type] = true;

        VanillaFallbackOnModDeletion = TileID.TeleportationPylon;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TEModdedPylon moddedPylon = ModContent.GetInstance<SimplePylonTileEntity>();
        TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(moddedPylon.PlacementPreviewHook_CheckIfCanPlace, 1, 0, true);
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(moddedPylon.Hook_AfterPlacement, -1, 0, false);

        TileObjectData.addTile(Type);

        TileID.Sets.InteractableByNPCs[Type] = true;

        AddToArray(ref TileID.Sets.CountsAsPylon);

        LocalizedText pylonName = CreateMapEntryName();
        AddMapEntry(Color.White, pylonName);
    }

    public override NPCShop.Entry GetNPCShopEntry()
    {
        NPCShop.Entry shopEntry = base.GetNPCShopEntry();
        shopEntry.AddCondition(ExampleConditions.InExampleBiome);
        return shopEntry;
    }

    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ModContent.ItemType<ExamplePylonItem>();
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        ModContent.GetInstance<SimplePylonTileEntity>().Kill(i, j);
    }

    public override bool ValidTeleportCheck_NPCCount(TeleportPylonInfo pylonInfo, int defaultNecessaryNPCCount)
    {
        return true;
    }

    public override bool ValidTeleportCheck_BiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData)
    {
        return ModContent.GetInstance<ExampleBiomeTileCount>().exampleBlockCount >= 40;
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = g = b = 0.75f;
    }

    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        DefaultDrawPylonCrystal(spriteBatch, i, j, crystalTexture, crystalHighlightTexture, new Vector2(0f, -12f), Color.White * 0.1f, Color.White, 1, CrystalVerticalFrameCount);
    }

    public override void DrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, TeleportPylonInfo pylonInfo, bool isNearPylon, Color drawColor, float deselectedScale, float selectedScale)
    {
        bool mouseOver = DefaultDrawMapIcon(ref context, mapIcon, pylonInfo.PositionInTiles.ToVector2() + new Vector2(1.5f, 2f), drawColor, deselectedScale, selectedScale);
        DefaultMapClickHandle(mouseOver, pylonInfo, ModContent.GetInstance<ExamplePylonItem>().DisplayName.Key, ref mouseOverText);
    }
}
