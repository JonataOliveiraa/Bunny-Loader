using System;
using System.IO;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsWall;

public sealed class TestWall : ModWall
{
    public static int Frames, Kills, Dusts, Sounds, Drops, Lights, Randoms, Animations, LastDrop;
    public static bool LastFail;

    public override void SetStaticDefaults()
    {
        Main.wallHouse[Type] = true;
        DustType = DustID.Stone;
        AddMapEntry(new Color(150, 150, 150));
    }

    public override void KillWall(int i, int j, ref bool fail)
    {
        Kills++;
        LastFail = fail;
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 2 : 5;

    public override bool CreateDust(int i, int j, ref int type)
    {
        Dusts++;
        return true;
    }

    public override bool KillSound(int i, int j, bool fail)
    {
        Sounds++;
        return true;
    }

    public override bool Drop(int i, int j, ref int type)
    {
        Drops++;
        LastDrop = type;
        return true;
    }

    public override bool WallFrame(int i, int j, bool randomizeFrame, ref int style, ref int frameNumber)
    {
        Frames++;
        if (randomizeFrame)
            frameNumber = 2;
        return true;
    }

    public override void AnimateWall(ref byte frame, ref byte frameCounter)
    {
        Animations++;
        if (++frameCounter >= 5)
        {
            frameCounter = 0;
            frame = (byte)(++frame % 2);
        }
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Lights++;
        r = 0.5f;
    }

    public override void RandomUpdate(int i, int j, bool underground) => Randoms++;
}

public sealed class TestWallPlain : ModWall
{
    public override void SetStaticDefaults()
    {
        AddMapEntry(new Color(90, 90, 90));
        RegisterItemDrop(ItemID.StoneBlock);
    }
}

public sealed class TestWallItem : ModItem
{
    public override void SetDefaults() => Item.DefaultToPlaceableWall(ModContent.WallType<TestWall>());
}

public sealed class CsWallMod : Mod
{
    static int frames, pass, fail, baseX, floor, lightsAt, animationsAt;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsWallMod>().Logger;

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
        if (done) Log.Info($"cswall END pass={pass} fail={fail}");
    }

    static int Type => ModContent.WallType<TestWall>();

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                baseX = Main.spawnTileX + 4;
                floor = Ground(baseX);
                Reopened();
                Registration();
                break;
            case 20:
                Clear();
                Placement();
                lightsAt = TestWall.Lights;
                animationsAt = TestWall.Animations;
                break;
            case 80:
                Report("AnimateWall runs every update and drives Main.wallFrame", TestWall.Animations > animationsAt && Main.wallFrame[Type] < 2,
                    $"calls={TestWall.Animations - animationsAt} frame={Main.wallFrame[Type]} counter={Main.wallFrameCounter[Type]}");
                Report("ModifyLight runs for the walls on screen", TestWall.Lights > lightsAt, $"calls={TestWall.Lights - lightsAt}");
                RandomUpdates();
                Killing();
                Persist();
                done = true;
                break;
        }
    }

    static int Ground(int x)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return y;
        }
        return Main.spawnTileY;
    }

    static void Clear()
    {
        for (int x = baseX; x <= baseX + 8; x++)
            for (int y = floor - 6; y < floor; y++)
                if (Main.tile[x, y].WallType != 0 && !(x == baseX && y == floor - 6))
                    WorldGen.KillWall(x, y, false);
        ClearItems();
    }

    static void ClearItems()
    {
        for (int k = 0; k < Main.item.Length; k++)
            if (Main.item[k].active)
                Main.item[k].TurnToAir();
    }

    static void Reopened()
    {
        Tile tile = Main.tile[baseX, floor - 6];
        if (tile.WallType == 0)
        {
            Log.Info("cswall persistence: no saved wall (first run)");
            return;
        }
        Report("the saved mod wall comes back", tile.WallType == Type, $"wall={tile.WallType} expected={Type}");
    }

    static void Registration()
    {
        Item item = Item.New();
        item.SetDefaults(ModContent.ItemType<TestWallItem>(), default);
        bool texture = TextureAssets.Wall[Type] != null;
        Report("the wall gets a type after the game's walls, its texture and SetStaticDefaults", Type >= 367 && WallLoader.GetWall(Type) is TestWall && texture && Main.wallHouse[Type] && WallLoader.WallCount > Type,
            $"type={Type} plain={ModContent.WallType<TestWallPlain>()} count={WallLoader.WallCount} texture={texture} house={Main.wallHouse[Type]}");
        Report("DefaultToPlaceableWall places the mod wall", item.createWall == Type, $"createWall={item.createWall}");
    }

    static void Placement()
    {
        int framesAt = TestWall.Frames;
        for (int x = baseX; x < baseX + 4; x++)
            for (int y = floor - 3; y < floor; y++)
                WorldGen.PlaceWall(x, y, Type, true);
        WorldGen.PlaceWall(baseX + 6, floor - 1, ModContent.WallType<TestWallPlain>(), true);
        Tile tile = Main.tile[baseX + 1, floor - 2];
        Report("PlaceWall puts the mod wall in the world", tile.WallType == Type && Main.tile[baseX + 6, floor - 1].WallType == ModContent.WallType<TestWallPlain>(), $"wall={tile.WallType}");
        Framing.WallFrame(baseX + 1, floor - 2, true);
        Report("WallFrame runs and its frame number is applied", TestWall.Frames > framesAt && tile.WallFrameNumber == 2,
            $"calls={TestWall.Frames - framesAt} number={tile.WallFrameNumber} frame={tile.WallFrameX},{tile.WallFrameY}");
    }

    static void RandomUpdates()
    {
        int at = TestWall.Randoms;
        WorldGen.UpdateWorld_OvergroundTile(baseX + 1, floor - 2, false, 3);
        WorldGen.UpdateWorld_OvergroundTile(baseX + 7, floor - 2, false, 3);
        Report("RandomUpdate runs only on the mod wall's cell", TestWall.Randoms == at + 1, $"calls={TestWall.Randoms - at}");
    }

    static int DroppedNear(int x, int y)
    {
        for (int k = 0; k < Main.item.Length; k++)
        {
            WorldItem item = Main.item[k];
            if (item.active && Math.Abs(item.position.X / 16 - x) < 4 && Math.Abs(item.position.Y / 16 - y) < 4)
                return item.type;
        }
        return 0;
    }

    static void Killing()
    {
        int x = baseX + 2, y = floor - 2;
        int kills = TestWall.Kills, dusts = TestWall.Dusts, sounds = TestWall.Sounds;
        WorldGen.KillWall(x, y, true);
        Report("a failed hit calls KillWall, NumDust's dust count and KillSound, and keeps the wall",
            Main.tile[x, y].WallType == Type && TestWall.Kills == kills + 1 && TestWall.LastFail && TestWall.Dusts == dusts + 2 && TestWall.Sounds == sounds + 1,
            $"kills={TestWall.Kills - kills} dusts={TestWall.Dusts - dusts} sounds={TestWall.Sounds - sounds}");
        ClearItems();
        dusts = TestWall.Dusts;
        int drops = TestWall.Drops;
        WorldGen.KillWall(x, y, false);
        int dropped = DroppedNear(x, y);
        Report("breaking it drops the item that places it", Main.tile[x, y].WallType == 0 && TestWall.Drops == drops + 1 && TestWall.LastDrop == ModContent.ItemType<TestWallItem>() && dropped == TestWall.LastDrop && TestWall.Dusts == dusts + 5,
            $"wall={Main.tile[x, y].WallType} drop={TestWall.LastDrop} dropped={dropped} dusts={TestWall.Dusts - dusts}");
        ClearItems();
        WorldGen.KillWall(baseX + 6, floor - 1, false);
        dropped = DroppedNear(baseX + 6, floor - 1);
        Report("RegisterItemDrop sets the dropped item", Main.tile[baseX + 6, floor - 1].WallType == 0 && dropped == ItemID.StoneBlock, $"dropped={dropped}");
        ClearItems();
    }

    static void Persist()
    {
        if (Main.tile[baseX, floor - 6].WallType == 0)
            WorldGen.PlaceWall(baseX, floor - 6, Type, true);
        WorldFile.SaveWorld();
        string file = Main.worldPathName + ".walls.bl";
        Report("the world's mod walls are saved apart", File.Exists(file), file);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cswall {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
