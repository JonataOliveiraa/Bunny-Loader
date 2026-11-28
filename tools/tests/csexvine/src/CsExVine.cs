using System;
using System.Text;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExVine;

public sealed class CsExVineMod : Mod
{
    const int End = 600, Watch = 240, Length = 5, Width = 6;

    static int frame, pass, fail, top, modLeft, gameLeft, strips;
    static int vineType, blockType;
    static readonly StringBuilder drawn = new();
    static int draws, emptyDraws, wrongUpdates, updates;

    static ILog Log => ModContent.GetInstance<CsExVineMod>().Logger;

    static bool Watching => frame > 100 && frame <= 100 + Watch;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DoDraw += DoDraw;
        On_TileDrawing.DrawVineStrip += Strip;
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        strips = 0;
        orig(self, gameTime);
        if (!Watching) return;

        draws++;
        if (strips == 0) emptyDraws++;
        if (drawn.Length < 90) drawn.Append(Math.Min(strips, 9));
    }

    static void Strip(On_TileDrawing.orig_DrawVineStrip orig, TileDrawing self, Vector2 screenPosition, Vector2 offSet, int x, int startY)
    {
        if (x >= modLeft && x < modLeft + Width && startY >= top - 2 && startY <= top + Length + 1) strips++;
        orig(self, screenPosition, offSet, x, startY);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexvine {name}: {(ok ? "ok" : "FAIL")} {detail}");
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
        if (i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csexvine END pass={pass} fail={fail}");
    }

    static void Step(Player player)
    {
        if (frame == 30)
        {
            Main.dayTime = true;
            Main.time = 27000;
        }

        if (frame == 40) Build(player);
        if (frame == 100) Log.Info("csexvine SHOT vines");
        if (Watching) Sample();
        if (frame == 100 + Watch) Check();
    }

    static void Build(Player player)
    {
        int ground = (int)((player.position.Y + player.height) / 16), cx = (int)(player.Center.X / 16);
        top = ground - 9;
        modLeft = cx - 3 - Width;
        gameLeft = cx + 4;
        blockType = ModContent.Find<ModTile>("ExampleMod/ExampleBlock").Type;
        vineType = ModContent.Find<ModTile>("ExampleMod/ExampleVine").Type;

        for (int x = modLeft - 1; x < gameLeft + Width + 1; x++)
            for (int y = top - 1; y <= top + Length + 1; y++)
                WorldGen.KillTile(x, y, false, false, true);

        Row(modLeft, blockType, vineType);
        Row(gameLeft, TileID.Grass, TileID.Vines);
    }

    static void Row(int left, int anchor, int vine)
    {
        for (int x = left; x < left + Width; x++)
        {
            Tile block = Main.tile[x, top];
            block.TileType = (ushort)anchor;
            block.HasTile = true;
            for (int y = top + 1; y <= top + Length; y++)
            {
                Tile cell = Main.tile[x, y];
                cell.TileType = (ushort)vine;
                cell.HasTile = true;
            }
        }

        for (int x = left - 1; x <= left + Width; x++)
            for (int y = top - 1; y <= top + Length + 1; y++)
                WorldGen.SquareTileFrame(x, y);
    }

    static void Sample()
    {
        updates++;
        for (int x = modLeft; x < modLeft + Width; x++)
            for (int y = top + 1; y <= top + Length; y++)
                if (!Main.tile[x, y].HasTile || Main.tile[x, y].TileType != vineType)
                {
                    wrongUpdates++;
                    return;
                }
    }

    static void Check()
    {
        Log.Info($"csexvine ExampleVine strips per draw: {drawn}");
        Report("ExampleVine cells keep their type", wrongUpdates == 0, $"updates with another type={wrongUpdates}/{updates}");
        Report("every column of ExampleVine is drawn in wind on every frame", draws > 0 && emptyDraws == 0 && drawn.ToString().Replace(Width.ToString(), "").Length == 0,
            $"draws={draws} empty={emptyDraws}");
    }
}
