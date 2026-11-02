using System;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsSmart;

public abstract class ChairBase : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(Type);
    }

    public override bool RightClick(int i, int j) => true;
}

public sealed class SmartChair : ChairBase
{
    public static int Asked;

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings)
    {
        Asked++;
        return base.HasSmartInteract(i, j, settings);
    }
}

public sealed class PlainChair : ChairBase
{
    public static int Asked;

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings)
    {
        Asked++;
        return false;
    }
}

public sealed class CsSmartMod : Mod
{
    static int frames, pass, fail, smartX, plainX, floor;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsSmartMod>().Logger;

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
        if (done) Log.Info($"cssmart END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                Player player = Main.LocalPlayer;
                smartX = (int)(player.Center.X / 16) + 2;
                plainX = smartX + 3;
                floor = FindFloor(smartX);
                for (int x = smartX - 1; x <= plainX + 1; x++)
                    for (int y = floor - 3; y < floor; y++)
                        WorldGen.KillTile(x, y, false, false, true);
                bool a = WorldGen.PlaceObject(smartX, floor - 1, ModContent.TileType<SmartChair>(), true);
                bool b = WorldGen.PlaceObject(plainX, floor - 1, ModContent.TileType<PlainChair>(), true);
                Report("the chairs are placed", a && b, $"smart={a} plain={b}");
                break;
            case 60:
                Log.Info($"cssmart game pick: x={Main.SmartInteractX} y={Main.SmartInteractY} genuine={Main.SmartInteractShowingGenuine} coords={Main.SmartInteractTileCoordsCount} smart={smartX},{floor - 2}..{floor - 1} asked={SmartChair.Asked}");
                Targets();
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

    static void Targets()
    {
        var provider = TileSmartInteractCandidateProvider.New();
        provider.ClearSelfAndPrepareForCheck();
        Player player = Main.LocalPlayer;
        var settings = new SmartInteractScanSettings
        {
            player = player,
            FullInteraction = true,
            mousevec = new Vector2(smartX * 16 + 8, (floor - 1) * 16 + 8),
            LX = smartX - 6,
            HX = plainX + 6,
            LY = floor - 8,
            HY = floor + 2
        };
        int smartAsked = SmartChair.Asked, plainAsked = PlainChair.Asked;
        bool found = provider.ProvideCandidate(settings, out _);
        var targets = new GameList(provider.targets);
        bool smart = targets.Contains(provider.JoinValue(smartX, floor - 1)) || targets.Contains(provider.JoinValue(smartX, floor - 2));
        bool plain = targets.Contains(provider.JoinValue(plainX, floor - 1)) || targets.Contains(provider.JoinValue(plainX, floor - 2));
        Report("a mod tile with RightClick is a smart interact target, one whose HasSmartInteract is false is not",
            smart && !plain && SmartChair.Asked > smartAsked && PlainChair.Asked > plainAsked, $"smart={smart} plain={plain} targets={targets.Count}");
        Report("the game's provider picks the mod tile", found, $"found={found}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssmart {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
