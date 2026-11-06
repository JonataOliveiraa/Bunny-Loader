using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsInvasion;

public sealed class TestInvasion : ModInvasion
{
    public static bool Allow = true, Fail;
    public static int Started, Completed, Failed, Updated;
    public static readonly List<int> WavesStarted = new(), WavesDone = new();

    public override string IconTexture => "CsInvasion/Icon";

    public override IList<InvasionWave> Waves { get; } = new[]
    {
        new InvasionWave(4, new InvasionEnemy(NPCID.BlueSlime, 1, 2)),
        new InvasionWave(3, new InvasionEnemy(NPCID.GreenSlime)),
    };

    public override int SpawnRate => 1;

    public override int MaxSpawns => 6;

    public override bool CanStart() => Allow;

    public override bool ShouldFail() => Fail;

    public override void OnStart() => Started++;

    public override void OnWaveStart(int wave) => WavesStarted.Add(wave);

    public override void OnWaveComplete(int wave) => WavesDone.Add(wave);

    public override void OnComplete() => Completed++;

    public override void OnFail() => Failed++;

    public override void Update() => Updated++;
}

public sealed class CsInvasionMod : Mod
{
    static int frame, pass, fail;

    static ILog Log => ModContent.GetInstance<CsInvasionMod>().Logger;

    static TestInvasion Test => ModContent.GetInstance<TestInvasion>();

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csinvasion {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        self.statLife = self.statLifeMax2;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static int Kill(Player self, int type, bool statue = false)
    {
        int index = NPC.NewNPC(self.GetSource_FromThis(), (int)self.Center.X + 300, (int)self.Center.Y - 100, type);
        NPC npc = Main.npc[index];
        npc.SpawnedFromStatue = statue;
        npc.StrikeInstantKill();
        return index;
    }

    static int Natural(int type)
    {
        int count = 0;
        for (int k = 0; k < Main.maxNPCs; k++)
            if (Main.npc[k].active && Main.npc[k].type == type)
                count++;
        return count;
    }

    static void ClearHostile()
    {
        for (int k = 0; k < Main.maxNPCs; k++)
            if (Main.npc[k].active && !Main.npc[k].friendly)
                Main.npc[k].active = false;
    }

    static List<ModSystem> Systems()
    {
        var found = new List<ModSystem>();
        foreach (Mod mod in ModLoader.Mods)
            found.AddRange(mod.GetContent<ModSystem>().Where(s => s.GetType().Name == "InvasionSystem"));
        return found;
    }

    static Dictionary<ModSystem, TagCompound> SaveAll()
    {
        var tags = new Dictionary<ModSystem, TagCompound>();
        foreach (ModSystem system in Systems())
        {
            var tag = new TagCompound();
            system.SaveWorldData(tag);
            tags[system] = tag;
        }
        return tags;
    }

    static void ClearAll()
    {
        foreach (ModSystem system in Systems())
            system.ClearWorld();
    }

    static void LoadAll(Dictionary<ModSystem, TagCompound> tags)
    {
        foreach (var (system, tag) in tags)
            system.LoadWorldData(tag);
    }

    static void Step(Player self)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 10000;
                ClearHostile();
                TestInvasion.Allow = false;
                bool refused = !InvasionLoader.CanStart(Test) && !InvasionLoader.Start(Test);
                Report("CanStart can refuse the invasion", refused && InvasionLoader.Active == null);
                TestInvasion.Allow = true;
                Report("CanStart passes, and the invasion is registered", InvasionLoader.CanStart(Test) && Test.Type >= 0 && InvasionLoader.All.Contains(Test));
                Main.invasionType = 1;
                Report("a vanilla invasion blocks the start", !InvasionLoader.CanStart(Test));
                Main.invasionType = 0;
                Report("Start begins the first wave", InvasionLoader.Start(Test) && InvasionLoader.Active == Test && InvasionLoader.Wave == 0 && TestInvasion.Started == 1 && TestInvasion.WavesStarted.SequenceEqual(new[] { 0 }), $"max={InvasionLoader.ProgressMax}");
                Report("the wave's points come from Waves and ScalePoints", InvasionLoader.ProgressMax == 4, $"max={InvasionLoader.ProgressMax}");
                Report("a second start is refused while one is active", !InvasionLoader.Start(Test) && !InvasionLoader.CanStart(Test));
                Report("vanilla invasions cannot start during a mod invasion", !Main.CanStartInvasion(1, true));
                Main.StartInvasion(1);
                Main.startPumpkinMoon();
                Main.startSnowMoon();
                Report("vanilla events do not start during a mod invasion", Main.invasionType == 0 && !Main.pumpkinMoon && !Main.snowMoon);
                break;
            case 200:
                int blue = Natural(NPCID.BlueSlime);
                int others = 0;
                for (int k = 0; k < Main.maxNPCs; k++)
                    if (Main.npc[k].active && !Main.npc[k].friendly && Main.npc[k].type != NPCID.BlueSlime)
                        others++;
                Report("EditSpawnPool replaces the spawns with the wave's enemies", blue > 0 && others == 0, $"blue={blue} others={others}");
                Report("the bar values follow the state", Main.invasionProgressMax == 4 && Main.invasionProgressWave == 1 && Main.invasionWaveMax == 2 && Main.invasionProgressIcon == 3 && Main.invasionProgressDisplayLeft > 0, $"max={Main.invasionProgressMax} wave={Main.invasionProgressWave}/{Main.invasionWaveMax} icon={Main.invasionProgressIcon}");
                Report("Update runs every tick on the server", TestInvasion.Updated > 100, $"updates={TestInvasion.Updated}");
                ClearHostile();
                break;
            case 210:
                int before = InvasionLoader.Progress;
                Kill(self, NPCID.BlueSlime, true);
                Report("a statue kill gives no points", InvasionLoader.Progress == before, $"progress={InvasionLoader.Progress}");
                Kill(self, NPCID.Zombie);
                Report("an enemy outside the wave gives no points", InvasionLoader.Progress == before, $"progress={InvasionLoader.Progress}");
                Kill(self, NPCID.BlueSlime);
                Report("a kill gives the enemy's points", InvasionLoader.Progress == before + 2, $"progress={InvasionLoader.Progress}");
                break;
            case 220:
                var tags = SaveAll();
                ClearAll();
                bool cleared = InvasionLoader.Active == null;
                LoadAll(tags);
                Report("saving and loading keeps the wave and progress", cleared && InvasionLoader.Active == Test && InvasionLoader.Wave == 0 && InvasionLoader.Progress == 2 && InvasionLoader.ProgressMax == 4, $"wave={InvasionLoader.Wave} progress={InvasionLoader.Progress}");
                break;
            case 230:
                Kill(self, NPCID.BlueSlime);
                Report("the wave changes when its points are reached", InvasionLoader.Wave == 1 && InvasionLoader.Progress == 0 && InvasionLoader.ProgressMax == 3 && TestInvasion.WavesDone.SequenceEqual(new[] { 0 }) && TestInvasion.WavesStarted.SequenceEqual(new[] { 0, 1 }), $"wave={InvasionLoader.Wave} max={InvasionLoader.ProgressMax}");
                break;
            case 240:
                Report("the game's bar advances to the second wave", Main.invasionProgressWave == 2 && Main.invasionProgressMax == 3, $"wave={Main.invasionProgressWave}");
                InvasionLoader.AddPoints(3);
                Report("finishing the last wave completes the invasion", InvasionLoader.Active == null && TestInvasion.Completed == 1 && InvasionLoader.IsDowned(Test), $"active={InvasionLoader.Active?.Name} completed={TestInvasion.Completed}");
                ClearHostile();
                break;
            case 300:
                var downedTags = SaveAll();
                ClearAll();
                bool forgotten = !InvasionLoader.IsDowned(Test);
                LoadAll(downedTags);
                Report("the downed flag is saved", forgotten && InvasionLoader.IsDowned(Test));
                InvasionLoader.Start(Test);
                TestInvasion.Fail = true;
                break;
            case 310:
                TestInvasion.Fail = false;
                Report("ShouldFail ends the invasion in failure", InvasionLoader.Active == null && TestInvasion.Failed == 1, $"failed={TestInvasion.Failed}");
                Main.startPumpkinMoon();
                bool moon = Main.pumpkinMoon;
                Main.pumpkinMoon = false;
                Report("vanilla events start again afterwards", moon);
                ClearHostile();
                Sample();
                break;
            case 700:
                if (InvasionLoader.Active != null)
                    InvasionLoader.Stop(false);
                Log.Info($"csinvasion END pass={pass} fail={fail}");
                break;
        }
    }

    static void Sample()
    {
        if (!ModContent.TryFind("ExampleMod", "GraveLegion", out ModInvasion grave) || !ModContent.TryFind("ExampleMod", "PaladinCrusade", out ModInvasion paladin))
        {
            Log.Info("csinvasion SHOT bar");
            InvasionLoader.Start(Test);
            return;
        }
        Main.dayTime = true;
        Report("the Paladin Crusade only starts at night", !InvasionLoader.CanStart(paladin));
        Report("the Grave Legion has three waves", grave.Waves.Count == 3 && grave.Waves[2].Enemies.Length == 4);
        Report("the sample's items have text", new Item(ModContent.Find<ModItem>("ExampleMod/GraveBell").Type).Name.Length > 0 && !new Item(ModContent.Find<ModItem>("ExampleMod/ConsecratedAxe").Type).Name.Contains("Mods."));
        Report("the invasion has its display name", !grave.DisplayName.Value.Contains("Mods.") && grave.DisplayName.Value.Length > 0, grave.DisplayName.Value);
        InvasionLoader.Start(grave);
        Report("the Grave Legion starts", InvasionLoader.Active == grave);
        Log.Info("csinvasion SHOT bar");
    }
}
