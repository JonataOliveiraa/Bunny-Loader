using System;
using BunnyLoader.Interop;
using CsNpcSpawn.Content;
using Terraria;
using Terraria.ModLoader;

namespace CsNpcSpawn;

public static class Checks
{
    public static int X, Y;
    public static bool Running;
    static int frames, pass, fail;
    static bool done;
    public static void Reset() { frames = pass = fail = 0; done = false; }
    public static void Frame(Player player)
    {
        if (done || ++frames != 120) return;
        done = true;
        bool day = Main.dayTime;
        double time = Main.time, surface = Main.worldSurface, rock = Main.rockLayer;
        try { Main.dayTime = true; Main.time = 30000; Run(player); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Running = false; Main.dayTime = day; Main.time = time; Main.worldSurface = surface; Main.rockLayer = rock; Log($"csnpcspawn END pass={pass} fail={fail}"); }
    }

    static void Run(Player player)
    {
        Running = true;
        X = (int)player.Center.X / 16 + 30;
        Y = (int)player.Bottom.Y / 16;
        Main.worldSurface = Y + 50; Main.rockLayer = Y + 100;
        var spawner = NPC.Spawner.New();
        using var pin = new GameHandle(spawner.Pointer);
        spawner.SetSpawnFlags(player);
        spawner.GetSpawnRate(player, out int rate, out int max);
        Report("EditSpawnRate runs on the native spawner", rate == 7 && max == 11, $"rate={rate} max={max}");
        NPC.Spawner.GetSpawnArea(player, out var area, out var safe);
        Report("EditSpawnRange updates both native rectangles", area.Width == 60 && area.Height == 40 && safe.Width == 16 && safe.Height == 12 && NPC.safeRangeX == 8 && NPC.safeRangeY == 6, $"spawn={area.Width}x{area.Height} safe={safe.Width}x{safe.Height}");
        spawner.SetSpawnFlagsForChosenTile(X, Y, Y, 2, 0);
        Report("EditSpawnFlags and EditSpawnInfo receive live native state", SpawnGlobal.FlagsCalls > 0 && SpawnGlobal.InfoCalls > 0 && spawner.nearGranite, $"flags={SpawnGlobal.FlagsCalls} info={SpawnGlobal.InfoCalls}");
        spawner.skyMob = spawner.invaders = spawner.spawnFriendly = spawner.waterTile = spawner.spawnSpider = spawner.spawnUndergroundDesert = false;
        spawner.nearGranite = true;
        int a = ModContent.NPCType<SpawnNpc>(), b = ModContent.NPCType<LegacyNpc>();
        int countA = 0, countB = 0;
        for (int attempt = 0; attempt < 256; attempt++)
        {
            spawner.SpawnAnNPC(X, Y, 2, 0, false, player.whoAmI);
            Collect(a, ref countA, b, ref countB);
        }
        Report("256 native spawn attempts create exactly 256 mod NPCs", countA + countB == 256, $"a={countA} b={countB}");
        Report("SpawnChance weights produce the expected 3 to 1 distribution", countA > 147 && countA < 237 && countB > 19, $"a={countA} b={countB}");
        Report("native and compatibility SpawnChance receive the selected tile and player", SpawnNpc.Chances >= 256 && LegacyNpc.LegacyChances >= 256 && SpawnNpc.ContextValid, $"modern={SpawnNpc.Chances} legacy={LegacyNpc.LegacyChances} valid={SpawnNpc.ContextValid}");
        Report("custom SpawnNPC runs before the applicable global", SpawnGlobal.Observed == 256 && SpawnGlobal.ContextValid, $"own={SpawnNpc.Creates} global={SpawnGlobal.Observed} valid={SpawnGlobal.ContextValid}");
        SpawnGlobal.Mode = 1;
        countA = countB = 0;
        for (int attempt = 0; attempt < 32; attempt++) { spawner.SpawnAnNPC(X, Y, 2, 0, false, player.whoAmI); Collect(a, ref countA, b, ref countB); }
        Report("EditSpawnPool can replace all mod weights", countA == 0 && countB == 32, $"a={countA} b={countB}");
        SpawnGlobal.Mode = 2;
        countA = countB = 0;
        for (int attempt = 0; attempt < 4; attempt++) { spawner.SpawnAnNPC(X, Y, 2, 0, false, player.whoAmI); Collect(a, ref countA, b, ref countB); }
        Report("an empty pool produces no NPC", countA + countB == 0, $"a={countA} b={countB}");
        SpawnGlobal.Mode = 3;
        Report("negative vanilla net IDs remain valid pool entries", NPCLoader.ChooseSpawn(spawner) == -4, "");
        SpawnGlobal.Mode = 4;
        Report("the zero entry selects the native spawn branch", NPCLoader.ChooseSpawn(spawner) == 0, "");
        SpawnGlobal.Mode = 5;
        Report("non-finite and negative weights cannot corrupt selection", !NPCLoader.ChooseSpawn(spawner).HasValue, "");
    }

    static void Collect(int a, ref int countA, int b, ref int countB)
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active) continue;
            if (npc.type == a) countA++;
            else if (npc.type == b) countB++;
            else continue;
            npc.active = false;
            NPC.spawnSlotProtected[i] = 0;
        }
    }
    static void Report(string name, bool ok, string detail) { if (ok) pass++; else fail++; Log($"csnpcspawn {name}: {(ok ? "ok" : "FAIL")} {detail}"); }
    static void Log(string text) => ModContent.GetInstance<CsNpcSpawnMod>().Logger.Info(text);
}
