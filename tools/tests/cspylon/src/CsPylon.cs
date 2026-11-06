using System;
using System.Linq;
using System.Reflection;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ObjectData;

namespace CsPylon;

public sealed class TestPylonEntity : TEModdedPylon
{
}

public sealed class CountingPylon : GlobalPylon
{
    public static int Npc, Biome, Post, Icons, Place;
    public static bool RefusePlace;

    public override bool PreDrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, ref TeleportPylonInfo pylonInfo, ref bool isNearPylon, ref Color drawColor, ref float deselectedScale, ref float selectedScale)
    {
        Icons++;
        return true;
    }

    public override bool? PreCanPlacePylon(int x, int y, int tileType, TeleportPylonType pylonType)
    {
        Place++;
        return RefusePlace ? false : null;
    }

    public override bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount)
    {
        Npc++;
        return null;
    }

    public override bool? ValidTeleportCheck_PreBiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData)
    {
        Biome++;
        return null;
    }

    public override void PostValidTeleportCheck(TeleportPylonInfo destinationPylonInfo, TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool validNearbyPylonFound, ref string errorKey) => Post++;
}

public abstract class PylonBase : ModPylon
{
    public static int NpcChecks, BiomeChecks, MapIcons, Crystals;
    public static bool BiomeOk;
    public static int DestinationChecks, NearbyChecks;

    Asset<Texture2D> mapIcon = null!, crystal = null!;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.StyleHorizontal = true;
        TEModdedPylon entity = ModContent.GetInstance<TestPylonEntity>();
        TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(entity.PlacementPreviewHook_CheckIfCanPlace, 1, 0, true);
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(entity.Hook_AfterPlacement, -1, 0, false);
        TileObjectData.addTile(Type);
        AddToArray(ref TileID.Sets.CountsAsPylon);
        AddMapEntry(Color.White, CreateMapEntryName());
        mapIcon = ModContent.Request<Texture2D>(Texture + "_MapIcon");
        crystal = ModContent.Request<Texture2D>(Texture + "_Crystal");
    }

    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Crystals++;
        DefaultDrawPylonCrystal(spriteBatch, i, j, crystal, crystal, new Vector2(0f, -12f), Color.White * 0.1f, Color.White, 1, 8);
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<TestPylonEntity>().Kill(i, j);

    public override bool ValidTeleportCheck_NPCCount(TeleportPylonInfo pylonInfo, int defaultNecessaryNPCCount)
    {
        NpcChecks++;
        return true;
    }

    public override void DrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, TeleportPylonInfo pylonInfo, bool isNearPylon, Color drawColor, float deselectedScale, float selectedScale)
    {
        MapIcons++;
        bool mouseOver = DefaultDrawMapIcon(ref context, mapIcon, pylonInfo.PositionInTiles.ToVector2() + new Vector2(1.5f, 2f), drawColor, deselectedScale, selectedScale);
        DefaultMapClickHandle(mouseOver, pylonInfo, "Mods.CsPylon.Pylon", ref mouseOverText);
    }
}

public sealed class TestPylon : PylonBase
{
    public override void ValidTeleportCheck_NearbyPostCheck(TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool anyNearbyValidPylon, ref string errorKey) => NearbyChecks++;
}

public sealed class TestPylonFar : PylonBase
{
    public override bool ValidTeleportCheck_BiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData)
    {
        BiomeChecks++;
        return BiomeOk;
    }

    public override void ValidTeleportCheck_DestinationPostCheck(TeleportPylonInfo destinationPylonInfo, ref bool destinationPylonValid, ref string errorKey) => DestinationChecks++;

    public override void ModifyTeleportationPosition(TeleportPylonInfo destinationPylonInfo, ref Vector2 teleportationPosition) => teleportationPosition.X += 32;
}

public sealed class TestPylonItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestPylon>());
}

public sealed class TestPylonFarItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TestPylonFar>());
}

public sealed class CsPylonMod : Mod
{
    static int frames, pass, fail, nearX, farX, nearFloor, farFloor;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPylonMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"cspylon END pass={pass} fail={fail}");
    }

    static ModPylon Near => ModContent.GetInstance<TestPylon>();

    static ModPylon Far => ModContent.GetInstance<TestPylonFar>();

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                nearX = Main.spawnTileX + 6;
                farX = Main.spawnTileX + 28;
                nearFloor = FindFloor(nearX + 1);
                farFloor = FindFloor(farX + 1);
                Registration();
                break;
            case 20:
                Placement();
                break;
            case 50:
                PylonList();
                Shop();
                break;
            case 60:
                Teleport();
                break;
            case 80:
                Report("SpecialDraw draws the crystal with DefaultDrawPylonCrystal", PylonBase.Crystals > 0, $"calls={PylonBase.Crystals}");
                Map();
                break;
            case 120:
                Report("DrawMapIcon draws the mod pylons on the full map", PylonBase.MapIcons > 0, $"calls={PylonBase.MapIcons}");
        Report("GlobalPylon.PreDrawMapIcon runs before the icon", CountingPylon.Icons > 0, $"calls={CountingPylon.Icons}");
                Main.mapFullscreen = false;
                WorldGen.KillTile(nearX, nearFloor - 4);
                break;
            case 150:
                Breaking();
                done = true;
                break;
        }
    }

    static int FindFloor(int column)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[column, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return y;
        }
        return Main.spawnTileY;
    }

    static void Registration()
    {
        Report("pylon types come after the game's and count as pylons", (int)Near.PylonType >= 11 && (int)Far.PylonType == (int)Near.PylonType + 1 && TileID.Sets.CountsAsPylon.Contains(Near.Type) && TileID.Sets.CountsAsPylon.Contains(597),
            $"near={(int)Near.PylonType} far={(int)Far.PylonType} counts={string.Join(",", TileID.Sets.CountsAsPylon)}");
    }

    static bool Place(int left, int floor, int type)
    {
        for (int x = left - 1; x <= left + 3; x++)
            for (int y = floor - 6; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
        for (int x = left - 1; x <= left + 3; x++)
        {
            Tile ground = Main.tile[x, floor];
            ground.TileType = TileID.Stone;
            ground.HasTile = true;
            ground.slope(0);
            ground.halfBrick(false);
        }
        int originX = left + 1, originY = floor - 1;
        if (!TileObject.CanPlace(originX, originY, type, 0, 1, out TileObject data, false, default) || !TileObject.Place(data)) return false;
        TileObjectData.CallPostPlacementPlayerHook(originX, originY, type, 0, 1, 0, data);
        return true;
    }

    static void Placement()
    {
        bool near = Place(nearX, nearFloor, Near.Type), far = Place(farX, farFloor, Far.Type);
        bool entity = TileEntity.TryGet(nearX + 2, nearFloor - 1, out TestPylonEntity te) && te.Position.X == nearX && te.Position.Y == nearFloor - 4;
        Report("placing a pylon puts its TEModdedPylon at the top left", near && far && entity && te.TryGetModPylon(out ModPylon p) && p == Near,
            $"near={near} far={far} entity={entity}");
        bool second = TileObject.CanPlace(nearX + 20, nearFloor - 1, Near.Type, 0, 1, out _, false, default);
        Report("CanPlacePylon allows one pylon of a type", !second, $"second={second}");
        int asked = CountingPylon.Place;
        CountingPylon.RefusePlace = true;
        bool refused = !TileObject.CanPlace(nearX + 34, nearFloor - 1, TileID.TeleportationPylon, 0, 1, out _, false, default);
        CountingPylon.RefusePlace = false;
        Report("GlobalPylon.PreCanPlacePylon runs and can refuse", refused && CountingPylon.Place > asked, $"refused={refused} calls={CountingPylon.Place}");
    }

    static bool Listed(ModPylon pylon, int x, int y)
    {
        var list = new GameList(Main.PylonSystem._pylons);
        for (int k = 0; k < list.Count; k++)
        {
            TeleportPylonInfo info = list.Get<TeleportPylonInfo>(k);
            if (info.TypeOfPylon == pylon.PylonType && info.PositionInTiles.X == x && info.PositionInTiles.Y == y) return true;
        }
        return false;
    }

    static void PylonList()
    {
        bool listed = Listed(Near, nearX, nearFloor - 4) && Listed(Far, farX, farFloor - 4);
        Report("the game's pylon list has the mod pylons", listed && Main.PylonSystem.HasPylonOfType(Near.PylonType), $"listed={listed} count={new GameList(Main.PylonSystem._pylons).Count}");
        Report("the player is near a pylon", TeleportPylonsSystem.IsPlayerNearAPylon(Main.LocalPlayer), "");
    }

    static void Shop()
    {
        var shop = new NPCShop(NPCID.Guide, "CsPylonProbe");
        Type hooks = typeof(ModPylon).Assembly.GetType("Terraria.ModLoader.Core.PylonHooks", true)!;
        hooks.GetMethod("AddShopEntries", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { shop });
        bool sold = shop.Entries.Any(e => e.Item.type == ModContent.ItemType<TestPylonItem>()) && shop.Entries.Any(e => e.Item.type == ModContent.ItemType<TestPylonFarItem>());
        Report("GetNPCShopEntry adds the pylon item to town shops", sold, $"entries={shop.Entries.Count}");
    }

    static void Teleport()
    {
        Player player = Main.LocalPlayer;
        var target = new TeleportPylonInfo { PositionInTiles = new Point16(farX, farFloor - 4), TypeOfPylon = Far.PylonType };
        Vector2 start = player.position;
        PylonBase.BiomeOk = false;
        Main.PylonSystem.RequestTeleportation(target, player);
        bool stayed = Vector2.Distance(player.position, start) < 32;
        int biome = PylonBase.BiomeChecks;
        PylonBase.BiomeOk = true;
        Main.PylonSystem.RequestTeleportation(target, player);
        bool moved = Math.Abs(player.position.X / 16 - farX) < 6;
        Vector2 expected = target.PositionInTiles.ToWorldCoordinates() - new Vector2(0f, player.HeightOffsetBoost) + new Vector2(32, 0);
        Report("ModifyTeleportationPosition moves the arrival", Math.Abs(player.position.X - expected.X) < 40 && player.position.X >= target.PositionInTiles.ToWorldCoordinates().X, $"at={player.position.X} expected={expected.X}");
        Report("DestinationPostCheck and NearbyPostCheck run", PylonBase.DestinationChecks >= 2 && PylonBase.NearbyChecks >= 1, $"destination={PylonBase.DestinationChecks} nearby={PylonBase.NearbyChecks}");
        Report("GlobalPylon teleport checks run", CountingPylon.Npc >= 2 && CountingPylon.Biome >= 2 && CountingPylon.Post == 2, $"npc={CountingPylon.Npc} biome={CountingPylon.Biome} post={CountingPylon.Post}");
        Report("ValidTeleportCheck_BiomeRequirements can refuse the destination", stayed && biome >= 1, $"stayed={stayed} biomeChecks={biome}");
        Report("teleporting to a mod pylon checks NPCs through ValidTeleportCheck_NPCCount", moved && PylonBase.NpcChecks >= 2,
            $"moved={moved} at={player.position.X / 16:0},{player.position.Y / 16:0} npcChecks={PylonBase.NpcChecks}");
        player.Teleport(start, 0, 0);
    }

    static void Map()
    {
        bool opened = Near.RightClick(nearX + 1, nearFloor - 2);
        Report("touching the pylon opens the map from it", opened && Main.mapFullscreen && Main.MapPylonTile.X == nearX && Main.MapPylonTile.Y == nearFloor - 4,
            $"map={Main.mapFullscreen} from={Main.MapPylonTile.X},{Main.MapPylonTile.Y}");
    }

    static void Breaking()
    {
        bool gone = !TileEntity.TryGet(nearX, nearFloor - 4, out TestPylonEntity _);
        Report("breaking the pylon removes it from the game's list", gone && !Listed(Near, nearX, nearFloor - 4) && !Main.PylonSystem.HasPylonOfType(Near.PylonType) && Listed(Far, farX, farFloor - 4),
            $"entity gone={gone} listed={Listed(Near, nearX, nearFloor - 4)}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cspylon {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
