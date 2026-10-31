using System;
using System.Collections.Generic;
using CsCollide.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCollide;

public static class Checks
{
    static int pass, fail, frames, groundTile, platformTile;
    static float ground;
    static readonly int[] bolts = new int[5];
    static readonly List<Point> placed = new();
    static int cutAllowed = -1, cutBlocked = -1;
    static Point plantAllowed, plantBlocked;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsCollideMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 30) StartCollide(p);
            if (frames == 90) CheckCollide();
            if (frames == 100) StartCut(p);
            if (frames == 115) CheckCut();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            Cleanup();
            Log.Info($"cscollide END pass={pass} fail={fail}");
        }
    }

    static void StartCollide(Player p)
    {
        ground = p.position.Y + p.height;
        groundTile = (int)(ground / 16f);
        platformTile = groundTile - 3;
        int center = (int)(p.Center.X / 16f);
        for (int x = center + 5; x <= center + 10; x++) Place(x, platformTile, TileID.Platforms);
        float[] offsets = { -96f, -48f, 48f, 104f, 152f };
        for (int mode = 0; mode < 5; mode++)
            bolts[mode] = Projectile.NewProjectile(p.GetSource_FromThis(), new Vector2(p.Center.X + offsets[mode], ground - 112f), new Vector2(0f, 6f),
                ModContent.ProjectileType<CollideBolt>(), 0, 0f, p.whoAmI, mode);
    }

    static float CollidedAt(int mode) => Main.projectile[bolts[mode]].ModProjectile is CollideBolt bolt ? bolt.CollidedY : float.NaN;

    static void CheckCollide()
    {
        float plain = CollidedAt(0), big = CollidedAt(1), platform = platformTile * 16f;
        Projectile bypass = Main.projectile[bolts[2]];
        Report("a bigger box from TileCollideStyle touches the ground earlier", Math.Abs(plain - (ground - 4f)) <= 6f && Math.Abs(plain - big - 16f) <= 6f,
            $"plain={plain} big={big} ground={ground}");
        Report("TileCollideStyle false skips the tile collision", float.IsNaN(CollidedAt(2)) && bypass.Center.Y > ground + 24f,
            $"collided={CollidedAt(2)} y={bypass.Center.Y} ground={ground}");
        Report("fallThrough false lands on a platform", Math.Abs(CollidedAt(3) - (platform - 4f)) <= 6f, $"y={CollidedAt(3)} platform={platform}");
        Report("the game's fallThrough reaches the hook and drops through platforms", CollideBolt.SeenFallThrough && Math.Abs(CollidedAt(4) - (ground - 4f)) <= 6f,
            $"seen={CollideBolt.SeenFallThrough} y={CollidedAt(4)} ground={ground}");
        foreach (int index in bolts)
            if (Main.projectile[index].active) Main.projectile[index].Kill();
    }

    static void StartCut(Player p)
    {
        int center = (int)(p.Center.X / 16f);
        plantAllowed = new Point(center - 6, groundTile - 1);
        plantBlocked = new Point(center - 9, groundTile - 1);
        Place(plantAllowed.X, plantAllowed.Y, TileID.Plants);
        Place(plantBlocked.X, plantBlocked.Y, TileID.Plants);
        CutterBolt.Order.Clear();
        CutterBolt.BlockedCuts = 0;
        cutAllowed = Spawn(p, plantAllowed, 0f);
        cutBlocked = Spawn(p, plantBlocked, 1f);
    }

    static int Spawn(Player p, Point tile, float mode) =>
        Projectile.NewProjectile(p.GetSource_FromThis(), new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f), Vector2.Zero, ModContent.ProjectileType<CutterBolt>(), 10, 0f, p.whoAmI, mode);

    static bool IsPlant(Point at) => Main.tile[at.X, at.Y].HasTile && Main.tile[at.X, at.Y].TileType == TileID.Plants;

    static void CheckCut()
    {
        bool placedBoth = placed.Contains(plantAllowed) && placed.Contains(plantBlocked);
        Report("CanCutTiles null lets the projectile cut grass", placedBoth && !IsPlant(plantAllowed), $"placed={placedBoth} cut={!IsPlant(plantAllowed)}");
        Report("CanCutTiles false keeps the grass and skips CutTiles", placedBoth && IsPlant(plantBlocked) && CutterBolt.BlockedCuts == 0,
            $"kept={IsPlant(plantBlocked)} blockedCuts={CutterBolt.BlockedCuts}");
        Report("CutTiles runs after the game's cut, globals before the ModProjectile",
            CutterBolt.Order.Count >= 2 && CutterBolt.Order[0] == "global" && CutterBolt.Order[1] == "mod", string.Join(",", CutterBolt.Order));
        done = true;
    }

    static void Place(int x, int y, int type)
    {
        if (Main.tile[x, y].HasTile) WorldGen.KillTile(x, y, false, false, true);
        if (WorldGen.PlaceTile(x, y, type, true, true)) placed.Add(new Point(x, y));
    }

    static void Cleanup()
    {
        foreach (int index in new[] { cutAllowed, cutBlocked })
            if ((uint)index < 1000u && Main.projectile[index].active) Main.projectile[index].Kill();
        foreach (Point at in placed)
            if (Main.tile[at.X, at.Y].HasTile) WorldGen.KillTile(at.X, at.Y, false, false, true);
        placed.Clear();
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cscollide {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
