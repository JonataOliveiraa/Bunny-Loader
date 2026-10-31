using System;
using System.IO;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsModSystem;

public static class Checks
{
    static int pass, fail, frames;
    static bool done, loadPhase;
    static double timeStart, normalDelta;

    static ILog Log => ModContent.GetInstance<CsModSystemMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        done = loadPhase = false;
    }

    public static void Frame()
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 20) loadPhase = TestSystem.HadData;
            if (frames == 30) Lifecycle();
            if (loadPhase && frames == 40) Loaded();
            if (!loadPhase && frames == 40) TestSystem.Recording = true;
            if (!loadPhase && frames == 43) Order();
            if (!loadPhase && frames == 50) timeStart = Main.time;
            if (!loadPhase && frames == 80)
            {
                normalDelta = Main.time - timeStart;
                TestSystem.TimeRate = 3;
                timeStart = Main.time;
            }
            if (!loadPhase && frames == 110) TimeRate();
            if (!loadPhase && frames == 120) Counts();
            if (!loadPhase && frames == 130) Save();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csmodsystem END pass={pass} fail={fail}");
    }

    static void Lifecycle()
    {
        Report("OnModLoad, ResizeArrays, OnLocalizationsLoaded and PostSetupRecipes run once at load",
            TestSystem.ModLoads == 1 && TestSystem.Resizes == 1 && TestSystem.Localizations >= 1 && TestSystem.SetupRecipes == 1,
            $"load={TestSystem.ModLoads} resize={TestSystem.Resizes} localization={TestSystem.Localizations} recipes={TestSystem.SetupRecipes}");
        Report("ClearWorld, CanWorldBePlayed and OnWorldLoad run when the world opens", TestSystem.Clears >= 1 && TestSystem.CanPlay >= 1 && TestSystem.WorldLoads == 1,
            $"clear={TestSystem.Clears} canPlay={TestSystem.CanPlay} load={TestSystem.WorldLoads}");
        Report("OnWorldLoad runs before LoadWorldData", TestSystem.LoadedBeforeData, "");
    }

    static void Order()
    {
        string[] expected =
        {
            "PreUpdateEntities", "PreUpdatePlayers", "PostUpdatePlayers", "PreUpdateNPCs", "PostUpdateNPCs", "PreUpdateGores", "PostUpdateGores",
            "PreUpdateProjectiles", "PostUpdateProjectiles", "PreUpdateItems", "PostUpdateItems", "PreUpdateDusts", "PostUpdateDusts", "PostUpdateEverything",
        };
        int at = -1;
        bool ordered = true;
        foreach (string hook in expected)
        {
            int index = TestSystem.Trace.IndexOf(hook);
            if (index <= at) ordered = false;
            at = Math.Max(at, index);
        }
        Report("the update hooks run in tModLoader's order within one frame", ordered, string.Join(",", TestSystem.Trace));
        string[] paired = { "PreUpdateTime", "PostUpdateTime", "PreUpdateWorld", "PostUpdateWorld", "PreUpdateInvasions", "PostUpdateInvasions", "PostUpdateInput" };
        string missing = "";
        foreach (string hook in paired)
            if (!TestSystem.Calls.ContainsKey(hook)) missing += hook + " ";
        Report("time, world, invasion and input hooks run every frame", missing == "", $"missing={missing}");
        Report("UpdateUI runs with the frame's GameTime", TestSystem.UIs > 0, $"calls={TestSystem.UIs}");
    }

    static void TimeRate()
    {
        double delta = Main.time - timeStart;
        TestSystem.TimeRate = 1;
        double ratio = normalDelta > 0 ? delta / normalDelta : 0;
        Report("ModifyTimeRate speeds up the clock", ratio > 2.5 && ratio < 3.5, $"normal={normalDelta:0.##} fast={delta:0.##} ratio={ratio:0.##}");
    }

    static void Counts()
    {
        Report("ResetNearbyTileEffects and TileCountsAvailable run on the player's scan", TestSystem.Resets > 0 && TestSystem.Counts > 0 && TestSystem.LastCountsLength >= TileID.Count,
            $"resets={TestSystem.Resets} counts={TestSystem.Counts} length={TestSystem.LastCountsLength}");
    }

    static string Key => $"{ModContent.GetInstance<TestSystem>().Mod.Uid}/TestSystem";

    static void Save()
    {
        TestSystem.SavedCount = 41;
        WorldFile.SaveWorld();
        string path = Main.worldPathName ?? "";
        string data = File.Exists(path + ".bl.json") ? File.ReadAllText(path + ".bl.json") : "";
        string header = File.Exists(path + ".bl.header.json") ? File.ReadAllText(path + ".bl.header.json") : "";
        Report("SaveWorldData writes the system's data next to the world", data.Contains(Key) && data.Contains("\"count\":41"), data.Length > 200 ? data[..200] : data);
        Report("SaveWorldHeader writes the header file", header.Contains(Key) && header.Contains("\"marker\":9"), header.Length > 200 ? header[..200] : header);
        bool found = Main.ActiveWorldFileData.TryGetHeaderData<TestSystem>(out TagCompound tag);
        Report("TryGetHeaderData reads the saved header", found && tag.GetInt("marker") == 9, $"found={found}");
        Log.Info("csmodsystem phase=save (run again with KEEPSAVES=1 to check the load)");
        done = true;
    }

    static void Loaded()
    {
        Report("LoadWorldData gets the saved data back", TestSystem.LoadedCount == 41, $"count={TestSystem.LoadedCount}");
        bool found = Main.ActiveWorldFileData.TryGetHeaderData<TestSystem>(out TagCompound tag);
        Report("TryGetHeaderData reads the header of the reopened world", found && tag.GetInt("marker") == 9, $"found={found}");
        Log.Info("csmodsystem phase=load");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmodsystem {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
