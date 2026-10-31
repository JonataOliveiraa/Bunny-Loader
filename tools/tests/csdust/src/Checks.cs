using System;
using CsDust.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDust;

public static class Checks
{
    static int pass, fail, frames, sheet = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsDustMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        sheet = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) Start(p);
            if (frames == 200) Check(p);
            if (frames > 60 && frames < 200 && frames % 10 == 0) Keep(p);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csdust END pass={pass} fail={fail}");
    }

    static int Spawn(Player p, int type) =>
        Dust.NewDust(p.Center + new Vector2(-40f, -80f), 80, 40, type);

    static void Start(Player p)
    {
        int test = ModContent.DustType<TestDust>(), sheetType = ModContent.DustType<SheetDust>(), draw = ModContent.DustType<DrawDust>();
        Report("types follow the game's dusts and lookups agree", Math.Min(test, Math.Min(sheetType, draw)) == DustID.Count && test + sheetType + draw == 3 * DustID.Count + 3 &&
            DustLoader.GetDust(test) == ModContent.GetInstance<TestDust>() && ModContent.GetModDust(draw) == ModContent.GetInstance<DrawDust>() &&
            DustLoader.DustCount >= DustID.Count + 3 && TestDust.StaticCalls == 1,
            $"types={test},{sheetType},{draw} count={DustLoader.DustCount} static={TestDust.StaticCalls}");

        var own = ModContent.GetInstance<TestDust>().Texture2D;
        var game = ModContent.GetInstance<SheetDust>().Texture2D;
        Report("Texture2D is the dust's own texture or the game's sheet", own != null && game != null && own.Value.Width == 10 && own.Value.Height == 30 &&
            game.Value.Width > 100,
            $"own={own?.Value.Width}x{own?.Value.Height} sheet={game?.Value.Width}");

        int spawned = 0;
        for (int i = 0; i < 10; i++)
            if (Spawn(p, test) < 6000) spawned++;
        Report("OnSpawn runs for each new dust with the frame at the texture's top", TestDust.Spawned == spawned && spawned > 0 && TestDust.LastFrameX == 0,
            $"spawned={spawned} calls={TestDust.Spawned} frameX={TestDust.LastFrameX}");
        sheet = Spawn(p, sheetType);
    }

    static void Keep(Player p)
    {
        Spawn(p, ModContent.DustType<SheetDust>());
        Spawn(p, ModContent.DustType<DrawDust>());
    }

    static void Check(Player p)
    {
        Report("Update replaces the game's update and dust.active = false frees the slot", TestDust.Updated > 10 && Alive(ModContent.DustType<TestDust>()) == 0,
            $"updates={TestDust.Updated} alive={Alive(ModContent.DustType<TestDust>())}");
        int fresh = 0;
        for (int i = 0; i < 20; i++)
            if (Spawn(p, ModContent.DustType<TestDust>()) < 6000) fresh++;
        Report("new dusts still spawn after the mod deactivated its own", fresh == 20, $"fresh={fresh}");

        int sheetType = ModContent.DustType<SheetDust>();
        Report("UpdateType runs the game's torch update and keeps the mod type", SheetDust.MidUpdates > 0 && Alive(sheetType) > 0,
            $"midUpdates={SheetDust.MidUpdates} alive={Alive(sheetType)}");
        Report("GetAlpha colors the drawn dust", SheetDust.Alphas > 0, $"alphas={SheetDust.Alphas}");
        Report("PreDraw runs before the dust draw", DrawDust.PreDraws > 0, $"preDraws={DrawDust.PreDraws}");
        done = true;
    }

    static int Alive(int type)
    {
        int count = 0;
        for (int i = 0; i < 6000; i++)
            if (Main.dust[i].active && Main.dust[i].type == type) count++;
        return count;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csdust {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
