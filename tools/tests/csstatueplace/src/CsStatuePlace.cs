using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsStatuePlace;

public sealed class StatueCountSystem : ModSystem
{
    public override void PostWorldGen()
    {
        int type = ModContent.Find<ModTile>("ExampleMod/ExampleStatue").Type, cells = 0;
        for (int x = 0; x < Main.maxTilesX; x++)
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.TileType == type) cells++;
            }
        ModContent.GetInstance<CsStatuePlaceMod>().Logger.Info($"csstatueplace generated world: {cells} statue cells, {cells / 6} statues");
    }
}

public sealed class CsStatuePlaceMod : Mod
{
    const int Width = 2;
    const int Height = 3;

    static int frame, pass, fail, kills;
    static bool blockKill;

    static ILog Log => ModContent.GetInstance<CsStatuePlaceMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_WorldGen.KillTile += Kill;
    }

    static void Kill(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
    {
        if (blockKill)
        {
            kills++;
            return;
        }
        orig(i, j, fail, effectOnly, noItem);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csstatueplace {name}: {(ok ? "ok" : "FAIL " + detail)}");
    }

    static int CellsOf(int type, int left, int top)
    {
        int cells = 0;
        for (int x = left - 1; x <= left + Width; x++)
            for (int y = top; y < top + Height; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.TileType == type) cells++;
            }
        return cells;
    }

    static void Clear(int left, int top)
    {
        WorldGen.destroyObject = true;
        for (int x = left - 1; x <= left + Width; x++)
            for (int y = top; y < top + Height; y++)
                Main.tile[x, y].active(false);
        WorldGen.destroyObject = false;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer || frame > 60) return;
        frame++;
        if (frame != 30) return;
        int ground = (int)((self.position.Y + self.height) / 16);
        int x = (int)(self.Center.X / 16) + 20;
        int top = ground - Height;
        int type = ModContent.Find<ModTile>("ExampleMod/ExampleStatue").Type;

        Clear(x, top);
        WorldGen.PlaceTile(x, ground - 1, type, true, true, -1, 0);
        int cells = CellsOf(type, x, top);
        Report("PlaceTile builds the whole statue", cells == Width * Height, $"cells={cells}");

        Clear(x, top);
        Tile lone = Main.tile[x, top];
        lone.active(true);
        lone.type = (ushort)type;
        lone.frameX = 0;
        lone.frameY = 0;
        blockKill = true;
        kills = 0;
        WorldGen.TileFrame(x, top, false, false);
        blockKill = false;
        Report("a block that cannot die does not recurse", kills == 1, $"kills={kills}");

        kills = 0;
        blockKill = true;
        lone.active(false);
        WorldGen.TileFrame(x, top, false, false);
        blockKill = false;
        Report("an inactive block with the statue's type is left alone", kills == 0, $"kills={kills}");

        Clear(x, top);
        Log.Info($"csstatueplace END pass={pass} fail={fail}");
    }
}
