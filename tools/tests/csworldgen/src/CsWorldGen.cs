using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using NativePass = BunnyLoader.Interop.WorldGen.GenPass;

namespace CsWorldGen;

public sealed class CsWorldGenMod : Mod
{
    public override void Load()
    {
        On_Main.DrawMenu += Menu;
        On_GUIWorldCreateMenu.Draw += CreateMenu;
        On_GUIWorldCreateMenu.CreateWorld += Create;
        On_WorldGenerator.RunPass += Pass;
        On_WorldFile.InternalSaveWorld += Save;
    }

    static void Menu(On_Main.orig_DrawMenu orig, Main self, GameTime time)
    {
        orig(self, time);
        if (!Main.gameMenu || Main.menuMode != 6 || Checks.Opened)
            return;
        Checks.Opened = true;
        GUIInstance.Active.GUIWorldSelectMenu.ActionNew();
    }

    static void CreateMenu(On_GUIWorldCreateMenu.orig_Draw orig, GUIWorldCreateMenu menu)
    {
        orig(menu);
        if (Checks.Requested)
            return;
        Checks.Requested = true;
        menu.CreateWorldCheck();
    }

    static void Create(On_GUIWorldCreateMenu.orig_CreateWorld orig, GUIWorldCreateMenu menu)
    {
        Checks.Started = true;
        Checks.Creator = Environment.CurrentManagedThreadId;
        Main.newWorldName = Checks.WorldName;
        menu._worldName = Checks.WorldName;
        menu._worldSeed = "73377337";
        menu.worldSize = 0;
        menu.difficulty = 0;
        menu.evilType = 0;
        Checks.Log("csworldgen CREATE " + Checks.WorldName);
        orig(menu);
    }

    static GenPassResult Pass(On_WorldGenerator.orig_RunPass orig, WorldGenerator generator, NativePass pass)
    {
        GenPassResult result = orig(generator, pass);
        if (Checks.Generating && pass.Enabled && !(pass.Name ?? "").StartsWith("CS "))
        {
            Checks.NativeRuns++;
            if (Checks.NativeRuns % 20 == 0)
                Checks.Log($"csworldgen PROGRESS count={Checks.NativeRuns} pass={pass.Name}");
        }
        return result;
    }

    static void Save(On_WorldFile.orig_InternalSaveWorld orig, bool cloud, bool reset)
    {
        orig(cloud, reset);
        if (!Checks.Generated || Main.ActiveWorldFileData.Name != Checks.WorldName || Checks.Done)
            return;
        Checks.Check("saved world validates", WorldFile.IsValidWorld(Main.worldPathName, false));
        Checks.Check("saved marker", Checks.MarkerCount() == 12);
        Checks.Done = true;
        Checks.Log($"csworldgen END pass={Checks.Passed} fail={Checks.Failed}");
    }
}

public static class Checks
{
    public static readonly string WorldName = "BL_CSWorldGen_" + DateTime.UtcNow.ToString("HHmmss");

    public static readonly List<string> Events = new();

    public static bool Started;

    public static bool Opened;

    public static bool Requested;

    public static bool Generating;

    public static bool Generated;

    public static bool Done;

    public static int Creator;

    public static int Worker;

    public static int NativeRuns;

    public static int DisabledRuns;

    public static int Passed;

    public static int Failed;

    public static int ChestSlot = -1;

    public static bool HardmodeTesting;

    public static bool ListOnlyRan;

    public const double ExpectedWeight = 777777.25;

    public static void Log(string text) => ModContent.GetInstance<CsWorldGenMod>().Logger.Info(text);

    public static void Check(string name, bool result)
    {
        if (result)
            Passed++;
        else
            Failed++;
        Log($"csworldgen {name}: {(result ? "ok" : "FAIL")}");
    }

    public static void Record(string name, GenerationProgress progress, GameConfiguration configuration)
    {
        Check(name + " native arguments", !progress.IsNull && !configuration.IsNull);
        Check(name + " generation worker", Environment.CurrentManagedThreadId == Worker);
        Check(name + " ref total weight", progress.TotalWeight == ExpectedWeight);
        try
        {
            configuration.Get<int>("BunnyMissingConfigurationEntry");
            Check(name + " configuration lookup", false);
        }
        catch (KeyNotFoundException)
        {
            Check(name + " configuration lookup", true);
        }
        progress.Message = name;
        progress.Set(0.5);
        Events.Add(name);
    }

    public static int MarkerCount()
    {
        int count = 0;
        for (int x = Main.maxTilesX / 2 + 80; x < Main.maxTilesX / 2 + 84; x++)
        {
            for (int y = 100; y < 103; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.type == TileID.Copper)
                    count++;
            }
        }

        return count;
    }

    public static void CreateChest()
    {
        int type = ModContent.TileType<ChestTile>();
        for (int i = 0; i < Main.chest.Length; i++)
        {
            if (!Main.chest[i].IsNull)
                continue;
            ChestSlot = i;
            Chest chest = Chest.New(i, 64, 100, false);
            chest.x = 64;
            chest.y = 100;
            Main.chest[i] = chest;
            break;
        }

        for (int x = 64; x < 66; x++)
        {
            Main.tile[x, 102].ResetToType(TileID.Stone);
            for (int y = 100; y < 102; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ResetToType((ushort)type);
                tile.frameX = (short)((x - 64) * 18);
                tile.frameY = (short)((y - 100) * 18);
            }
        }

        Check("mod chest fixture", ChestSlot >= 0);
    }

    public static void VerifyChest()
    {
        int count = 0;
        for (int x = 64; x < 66; x++)
        {
            for (int y = 100; y < 102; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.type == ModContent.TileType<ChestTile>())
                    count++;
            }
        }

        Check("Tile Cleanup keeps mod chest type", count == 4);
        if (ChestSlot >= 0)
            Main.chest[ChestSlot] = default;
    }
}

public sealed class BeforePass : GenPass
{
    public BeforePass() : base("CS Before", 2)
    {
    }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        Checks.Record("before", progress, configuration);
        ConfigurationChecks.Run();
    }
}

public sealed class DisabledPass : GenPass
{
    public DisabledPass() : base("CS Disabled", 1)
    {
    }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        Checks.DisabledRuns++;
    }
}

public sealed class GenerationSystem : ModSystem
{
    public override void PreWorldGen()
    {
        Checks.Generating = true;
        Checks.Worker = Environment.CurrentManagedThreadId;
        Checks.Events.Add("pre");
        Checks.Check("normal worker thread", Checks.Creator != Checks.Worker);
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        Checks.Check("full native task list", tasks.Count > 50);
        Checks.Check("native pass wrapper", tasks.All(pass => pass.Name.Length > 0 && pass.Weight >= 0));
        var disabled = new DisabledPass();
        disabled.Disable();
        Checks.Check("disabled pass flag", !disabled.Enabled);
        disabled.Enable();
        Checks.Check("enabled pass flag", disabled.Enabled);
        disabled.Disable();
        tasks.Insert(0, disabled);
        tasks.Insert(0, new BeforePass());
        int cleanup = tasks.FindIndex(pass => pass.Name == "Tile Cleanup");
        Checks.Check("native Tile Cleanup exists", cleanup >= 0);
        if (cleanup >= 0)
        {
            var weightProbe = new PassLegacy("Tile Cleanup", (progress, configuration) => { });
            Checks.Check("legacy default weight table", weightProbe.Weight == tasks[cleanup].Weight);
            tasks.Insert(cleanup, new PassLegacy("CS Chest Fixture", (progress, configuration) => Checks.CreateChest()));
            tasks.Insert(cleanup + 2, new PassLegacy("CS Chest Check", (progress, configuration) => Checks.VerifyChest()));
        }

        tasks.Add(new PassLegacy("CS Marker", (progress, configuration) =>
        {
            Checks.Record("marker", progress, configuration);
            for (int x = Main.maxTilesX / 2 + 80; x < Main.maxTilesX / 2 + 84; x++)
            {
                for (int y = 100; y < 103; y++)
                    Main.tile[x, y].ResetToType(TileID.Copper);
            }

            Checks.Check("custom pass marker", Checks.MarkerCount() == 12);
        }, 4));
        totalWeight = Checks.ExpectedWeight;
    }

    public override void PostWorldGen()
    {
        Checks.Events.Add("post");
        Checks.Check("custom pass order and post", string.Join(",", Checks.Events) == "pre,before,marker,post");
        Checks.Check("disabled pass skipped", Checks.DisabledRuns == 0);
        Checks.Check("native terrain passes preserved", Checks.NativeRuns > 50);
        Checks.Check("list-only overload dispatch", Checks.ListOnlyRan);
        Checks.Check("post sees marker", Checks.MarkerCount() == 12);
        Checks.HardmodeTesting = true;
        WorldGen.initializeHardMode();
        Checks.HardmodeTesting = false;
        Checks.Check("hardmode custom pass order", string.Join(",", Checks.Events).EndsWith("hard-before,hard-after"));
        Checks.Generated = true;
        Checks.Generating = false;
        Checks.Log($"csworldgen GENERATED pass={Checks.Passed} fail={Checks.Failed} native={Checks.NativeRuns}");
    }

    public override void ModifyHardmodeTasks(List<GenPass> list)
    {
        if (!Checks.HardmodeTesting)
            return;
        Checks.Check("mobile hardmode conversion stage", list.Count == 1 && list[0].Name == "Hardmode Conversion");
        list[0].Disable();
        list.Insert(0, new PassLegacy("CS Hard Before", (progress, configuration) => Checks.Events.Add("hard-before")));
        list.Add(new PassLegacy("CS Hard After", (progress, configuration) => Checks.Events.Add("hard-after")));
    }
}

public sealed class ListOnlySystem : ModSystem
{
    public override void ModifyWorldGenTasks(List<GenPass> tasks)
    {
        tasks.Add(new PassLegacy("CS List Only", (progress, configuration) => Checks.ListOnlyRan = true, 0));
    }
}

public sealed class ChestTile : ModTile
{
    public override string Texture => "CsWorldGen/ChestTile";

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.BasicChest[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(150, 150, 150));
    }
}
