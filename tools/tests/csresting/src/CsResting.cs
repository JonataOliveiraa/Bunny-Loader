using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsResting;

public sealed class TestChair : ModTile
{
    public static int Calls;
    public static bool PlayerSeen;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileID.Sets.CanBeSatOnForNPCs[Type] = true;
        TileID.Sets.CanBeSatOnForPlayers[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinatePaddingFix = new Point16(0, 2);
        TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
        TileObjectData.newTile.StyleWrapLimit = 2;
        TileObjectData.newTile.StyleMultiplier = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(Type);
    }

    public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info)
    {
        Calls++;
        PlayerSeen |= info.RestingEntity.Pointer == Main.LocalPlayer.Pointer;
        Tile tile = Framing.GetTileSafely(i, j);
        info.TargetDirection = tile.TileFrameX != 0 ? 1 : -1;
        info.AnchorTilePosition.X = i;
        info.AnchorTilePosition.Y = j;
        if (tile.TileFrameY % 40 == 0)
            info.AnchorTilePosition.Y++;
    }
}

public sealed class TestBed : ModTile
{
    public static int Calls;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.CanBeSleptIn[Type] = true;
        TileID.Sets.IsValidSpawnPoint[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style4x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinatePaddingFix = new Point16(0, -2);
        TileObjectData.addTile(Type);
    }

    public override void ModifySleepingTargetInfo(int i, int j, ref TileRestingInfo info)
    {
        Calls++;
        info.VisualOffset.Y += 4f;
    }
}

public sealed class CsRestingMod : Mod
{
    static int frames, pass, fail, chairX, bedX, floor, chairCalls;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsRestingMod>().Logger;

    static bool anyRoom;

    public override void Load()
    {
        On_Player.Update += Update;
        On_WorldGen.StartRoomCheck += (orig, x, y, feedback) => anyRoom || orig(x, y, feedback);
    }

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
        if (done) Log.Info($"csresting END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        Player player = Main.LocalPlayer;
        switch (f)
        {
            case 10:
                chairX = (int)(player.Center.X / 16) + 1;
                bedX = chairX + 4;
                floor = FindFloor(chairX);
                Place();
                break;
            case 20:
                player.sitting.SitDown(player, chairX, floor - 2);
                Sat(player);
                chairCalls = TestChair.Calls;
                break;
            case 40:
                Report("the sitting info is checked again while seated", player.sitting.isSitting && TestChair.Calls > chairCalls, $"calls={TestChair.Calls - chairCalls}");
                player.sitting.SitUp(player, false);
                break;
            case 50:
                player.sleeping.StartSleeping(player, bedX + 1, floor - 2);
                Slept(player);
                break;
            case 60:
                player.sleeping.StopSleeping(player, false);
                Spawn();
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

    static void Place()
    {
        for (int x = chairX - 1; x <= bedX + 4; x++)
            for (int y = floor - 4; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
        bool chair = WorldGen.PlaceObject(chairX, floor - 1, ModContent.TileType<TestChair>(), true);
        bool bed = WorldGen.PlaceObject(bedX + 1, floor - 1, ModContent.TileType<TestBed>(), true);
        Report("the chair and the bed are placed", chair && bed && Main.tile[chairX, floor - 2].TileType == ModContent.TileType<TestChair>() && Main.tile[bedX, floor - 1].TileType == ModContent.TileType<TestBed>(),
            $"chair={chair} bed={bed}");
    }

    static void Sat(Player player)
    {
        float expectedX = chairX * 16 + 8 - 6;
        float expectedBottom = floor * 16;
        Report("sitting calls ModifySittingTargetInfo and uses its direction and anchor", player.sitting.isSitting && TestChair.Calls > 0 && TestChair.PlayerSeen && player.direction == -1 &&
            Math.Abs(player.Bottom.X - expectedX) < 1.5f && Math.Abs(player.Bottom.Y - expectedBottom) < 1.5f,
            $"sitting={player.sitting.isSitting} calls={TestChair.Calls} player={TestChair.PlayerSeen} dir={player.direction} bottom={player.Bottom.X},{player.Bottom.Y} expected={expectedX},{expectedBottom}");
    }

    static void Slept(Player player)
    {
        Report("sleeping calls ModifySleepingTargetInfo and applies its visual offset", player.sleeping.isSleeping && TestBed.Calls > 0 && player.sleeping.visualOffsetOfBedBase.Y > 0,
            $"sleeping={player.sleeping.isSleeping} calls={TestBed.Calls} offset={player.sleeping.visualOffsetOfBedBase.X},{player.sleeping.visualOffsetOfBedBase.Y}");
    }

    static void Spawn()
    {
        int spawnX = bedX + 2, spawnY = floor;
        anyRoom = true;
        bool valid = Player.CheckSpawn(spawnX, spawnY);
        TileID.Sets.IsValidSpawnPoint[ModContent.TileType<TestBed>()] = false;
        bool unmarked = Player.CheckSpawn(spawnX, spawnY);
        TileID.Sets.IsValidSpawnPoint[ModContent.TileType<TestBed>()] = true;
        anyRoom = false;
        Report("a bed in TileID.Sets.IsValidSpawnPoint is a spawn point (room check bypassed)", valid && !unmarked && Main.tile[spawnX, spawnY - 1].TileType == ModContent.TileType<TestBed>(), $"valid={valid} unmarked={unmarked} type={Main.tile[spawnX, spawnY - 1].TileType}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csresting {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
