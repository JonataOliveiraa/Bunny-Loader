using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Terraria.ModLoader.Core;

internal static class SubworldTravel
{
    const int MaxWidth = 8400, MaxHeight = 2400;

    sealed class Trip
    {
        internal readonly ModSubworld? From, To;
        internal bool Started;

        internal Trip(ModSubworld? from, ModSubworld? to)
        {
            From = from;
            To = to;
        }
    }

    static volatile Trip? trip;
    static GameHandle? mainWorld, activeWorld;
    static Guid mainId;
    static int hiddenPrompts;
    static bool promptWasDisabled;
    static readonly Dictionary<string, TagCompound> carried = new(StringComparer.Ordinal);

    internal static bool Busy => trip != null;

    internal static void Install() =>
        ItemHooks.Once("Subworlds", () =>
        {
            On_WorldFile.LoadWorld += LoadWorld;
            On_WorldGen.FinaliseOnMainThread += Finalise;
            On_WorldGen.SaveAndQuitCallBack += SaveAndQuitCallBack;
            SubworldScreens.Install();
            SubworldWorldHooks.Install();
            SubworldNet.Install();
        });

    internal static bool Begin(ModSubworld? to)
    {
        if (trip != null || Main.gameMenu || to == SubworldLoader.Current || SubworldTransition.Active) return false;
        if (to != null && !Valid(to)) return false;
        if (SubworldNet.IsClient) return SubworldNet.Refuse();
        if (SubworldNet.IsHost) return SubworldNet.HostTravel(to);
        if (!RememberMain()) return false;
        var t = new Trip(SubworldLoader.Current, to);
        trip = t;
        SubworldTransition.Leave(to ?? SubworldLoader.Current, () => Swap(t));
        return true;
    }

    internal static bool BeginLocal(ModSubworld? to)
    {
        if (trip != null || Main.gameMenu || to == SubworldLoader.Current || !RememberMain()) return false;
        var t = new Trip(SubworldLoader.Current, to);
        trip = t;
        Swap(t);
        return true;
    }

    static bool RememberMain()
    {
        if (SubworldLoader.Current != null) return true;
        WorldFileData main = Main.ActiveWorldFileData;
        if (main.IsNull || main.IsCloudSave || string.IsNullOrEmpty(main.Path))
        {
            Log.Warn("Subworlds: the current world has no local file to come back to");
            return false;
        }

        mainWorld?.Dispose();
        mainWorld = new GameHandle(main.Pointer);
        mainId = main.UniqueId;
        return true;
    }

    static void Swap(Trip t)
    {
        if (trip != t) return;
        if (Main.gameMenu)
        {
            trip = null;
            SubworldTransition.Stop();
            return;
        }

        Main.menuMode = 10;
        Main.gameMenu = true;
        WorldGen.playWorld();
    }

    internal static string LoadingText
    {
        get
        {
            Trip? t = trip;
            ModSubworld? subworld = t?.To ?? t?.From;
            if (subworld != null)
            {
                try
                {
                    string text = subworld.LoadingText;
                    if (!string.IsNullOrEmpty(text)) return text;
                }
                catch (Exception e) { ItemHooks.Report(subworld.FullName + ".LoadingText", e); }
            }

            return Language.GetTextValue("BunnyLoader.LoadingDimension");
        }
    }

    static bool Valid(ModSubworld subworld)
    {
        try
        {
            int width = subworld.Width, height = subworld.Height;
            if (width > 0 && height > 0 && width <= MaxWidth && height <= MaxHeight) return true;
            Log.Error($"Subworlds: {subworld.FullName} is {width}x{height} tiles; sizes go from 1x1 to {MaxWidth}x{MaxHeight}");
        }
        catch (Exception e) { ItemHooks.Report(subworld.FullName + " size", e); }
        return false;
    }

    static void LoadWorld(On_WorldFile.orig_LoadWorld orig, bool canCheckFileState)
    {
        Trip? t = trip;
        if (t == null)
        {
            Forget();
            orig(canCheckFileState);
            return;
        }

        if (t.Started)
        {
            orig(canCheckFileState);
            return;
        }

        t.Started = true;
        try
        {
            Leave(t);
            Arrive(t);
        }
        catch (Exception e)
        {
            ItemHooks.Report("Subworld travel", e);
            BackToMain();
        }

        orig(canCheckFileState);
        if (WorldGen.loadFailed && SubworldLoader.Current is { } failed)
        {
            Log.Error($"Subworlds: {failed.FullName} could not be loaded; going back to the main world");
            BackToMain();
            orig(canCheckFileState);
        }

        if (!WorldGen.loadFailed) LoadCarried();
    }

    static void Finalise(On_WorldGen.orig_FinaliseOnMainThread orig, GameObject threadContext)
    {
        Trip? t = trip;
        if (t == null)
        {
            orig(threadContext);
            return;
        }

        ShowTouchControlsPrompt(false);
        try { orig(threadContext); }
        finally { ShowTouchControlsPrompt(true); }

        trip = null;
        ModSubworld? subworld = SubworldLoader.Current;
        if (subworld != null) Run(subworld, nameof(ModSubworld.PostWorldReady), subworld.PostWorldReady);
        SubworldTransition.Arrive();
    }

    static void SaveAndQuitCallBack(On_WorldGen.orig_SaveAndQuitCallBack orig, GameObject threadContext)
    {
        orig(threadContext);
        trip = null;
        Forget();
        SubworldNet.AfterSaveAndQuit();
        if (!SubworldNet.Rejoining) SubworldTransition.Stop();
    }

    internal static void ShowTouchControlsPrompt(bool show)
    {
        GUIInstance gui = GUIInstance.Active;
        GUIConfigureTouchControlsPopup popup = gui.IsNull ? default : gui.GUIConfigureTouchControlsPopup;
        if (popup.IsNull) return;
        if (!show)
        {
            hiddenPrompts++;
            if (hiddenPrompts == 1) promptWasDisabled = popup.DisableCheck;
            popup.DisableCheck = true;
            return;
        }

        if (hiddenPrompts == 0) return;
        hiddenPrompts--;
        if (hiddenPrompts == 0) popup.DisableCheck = promptWasDisabled;
    }

    internal static void SetRemote(ModSubworld? current)
    {
        if (SubworldNet.IsHost) return;
        SubworldLoader.Current = current;
        SubworldWorldHooks.Apply(current);
    }

    static void Forget()
    {
        SubworldLoader.Current = null;
        SubworldWorldHooks.Apply(null);
        mainWorld?.Dispose();
        mainWorld = null;
        activeWorld?.Dispose();
        activeWorld = null;
        lock (carried) carried.Clear();
    }

    static void Leave(Trip t)
    {
        foreach (ModSubworld subworld in SubworldLoader.Subworlds)
        {
            var tag = new TagCompound();
            try { subworld.SaveCarriedData(tag); }
            catch (Exception e) { ItemHooks.Report(subworld.FullName + ".SaveCarriedData", e); }
            lock (carried) carried[subworld.FullName] = tag;
        }

        if (t.From != null) Run(t.From, nameof(ModSubworld.OnExit), t.From.OnExit);
        PlayerFileData player = Main.ActivePlayerFileData;
        if (!SubworldWorldHooks.SavesPlayer) SubworldWorldHooks.ReloadPlayer();
        else if (!player.IsNull)
        {
            player.StopPlayTimer();
            Player.SavePlayer(player, false, true);
        }

        Rain.ClearRain();
        if (t.From == null || t.From.Persistent)
        {
            WorldFile.SetTempToOngoing();
            WorldFile.SaveWorld(WorldFile.WorldSaveContext.Normal);
        }

        SystemLoader.OnWorldUnload();
        if (t.From != null && !t.From.Persistent) DeleteFiles(PathOf(t.From));
    }

    static void Arrive(Trip t)
    {
        ModSubworld? to = t.To;
        if (to == null)
        {
            BackToMain();
            return;
        }

        SubworldLoader.Current = to;
        SubworldWorldHooks.Apply(to);
        Run(to, nameof(ModSubworld.OnEnter), to.OnEnter);
        string path = PathOf(to);
        if (to.Persistent && FileUtilities.Exists(path, false))
        {
            Activate(WorldFile.GetAllMetadata(path, false));
            return;
        }

        Generate(to, path);
    }

    static void BackToMain()
    {
        SubworldLoader.Current = null;
        SubworldWorldHooks.Apply(null);
        if (mainWorld == null) return;
        Main.ActiveWorldFileData = new WorldFileData(mainWorld.Target);
        activeWorld?.Dispose();
        activeWorld = null;
    }

    static void Activate(WorldFileData data)
    {
        activeWorld?.Dispose();
        activeWorld = new GameHandle(data.Pointer);
        Main.ActiveWorldFileData = data;
    }

    static string PathOf(ModSubworld subworld) => Path.Combine(Main.WorldPath ?? "", mainId.ToString(), subworld.FileName + ".subworld");

    static void Generate(ModSubworld subworld, string path)
    {
        string folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        FileUtilities.CreateDirectory(folder);
        DeleteFiles(path);
        WorldFileData main = new(mainWorld!.Target);
        string name = subworld.DisplayName.Value ?? subworld.Name;
        WorldFileData data = WorldFile.CreateMetadata(name, false, main.GameMode);
        data._path = path;
        data.UniqueId = IdOf(subworld);
        data.WorldId = (int)(Hash(data.UniqueId.ToString("N"), 0x9E3779B97F4A7C15UL) & int.MaxValue);
        Activate(data);
        DeleteMap(data);

        Main.worldName = name;
        Main.maxTilesX = subworld.Width;
        Main.maxTilesY = subworld.Height;
        WorldGen.setWorldSize();
        WorldGen.clearWorld();
        Main.worldSurface = Main.maxTilesY * 0.3;
        Main.rockLayer = Main.maxTilesY * 0.5;
        Main.spawnTileX = Main.maxTilesX / 2;
        Main.spawnTileY = (int)Main.worldSurface;
        Main.rand = UnifiedRandom.New(data.Seed);
        WorldGen.RandomizeTreeStyle();
        WorldGen.RandomizeCaveBackgrounds();
        WorldGen.RandomizeBackgrounds(WorldGen.genRand);
        WorldGen.RandomizeMoonState(WorldGen.genRand);
        RunTasks(subworld, data.Seed);
        WorldFile.SaveWorld(false, true, WorldFile.WorldSaveContext.Normal);
    }

    static void RunTasks(ModSubworld subworld, int seed)
    {
        List<GenPass> tasks = subworld.GenerationTasks ?? new List<GenPass>();
        GenerationProgress progress = new();
        using var progressHandle = new GameHandle(progress.Pointer);
        progress.TotalWeight = WorldGenHooks.WeightOf(tasks);
        WorldGenerator.CurrentGenerationProgress = progress;
        bool noMapUpdate = WorldGen.noMapUpdate, busy = WorldGen.isGeneratingOrLoadingWorld;
        WorldGen.noMapUpdate = true;
        WorldGen.isGeneratingOrLoadingWorld = true;
        WorldGen.generatingWorld = true;
        WorldGen.generatingWorldOnThisThread = true;
        try
        {
            foreach (GenPass pass in tasks)
            {
                if (!pass.Enabled) continue;
                Main.rand = UnifiedRandom.New(seed);
                progress.Start(pass.Weight);
                try { pass.Apply(progress, default); }
                catch (Exception e) { ItemHooks.Report($"{subworld.FullName} GenPass {pass.Name}", e); }
                progress.End();
            }
        }
        finally
        {
            WorldGen.generatingWorld = false;
            WorldGen.generatingWorldOnThisThread = false;
            WorldGen.noMapUpdate = noMapUpdate;
            WorldGen.isGeneratingOrLoadingWorld = busy;
            WorldGenerator.CurrentGenerationProgress = default;
            GC.KeepAlive(tasks);
        }
    }

    static void LoadCarried()
    {
        foreach (ModSubworld subworld in SubworldLoader.Subworlds)
        {
            TagCompound? tag;
            lock (carried)
                if (!carried.TryGetValue(subworld.FullName, out tag)) continue;
            try { subworld.LoadCarriedData(tag); }
            catch (Exception e) { ItemHooks.Report(subworld.FullName + ".LoadCarriedData", e); }
        }
    }

    static void Run(ModSubworld subworld, string hook, Action action)
    {
        try { action(); }
        catch (Exception e) { ItemHooks.Report($"{subworld.FullName}.{hook}", e); }
    }

    static void DeleteFiles(string path)
    {
        foreach (string file in new[] { path, path + ".bak", path + ".bad" })
            if (FileUtilities.Exists(file, false))
                FileUtilities.Delete(file, false);

        string? folder = Path.GetDirectoryName(path);
        if (folder == null || !Directory.Exists(folder)) return;
        string name = Path.GetFileName(path);
        foreach (string file in Directory.GetFiles(folder, name + "*"))
        {
            try { File.Delete(file); }
            catch (Exception e) { Log.Warn($"Subworlds: {file} could not be deleted ({e.Message})"); }
        }
    }

    static void DeleteMap(WorldFileData world)
    {
        PlayerFileData player = Main.ActivePlayerFileData;
        if (player.IsNull || string.IsNullOrEmpty(player.Path)) return;
        if (!WorldMap.TryGetMapPath(player, world, out GameString found)) return;
        string? map = found;
        if (!string.IsNullOrEmpty(map)) DeleteFiles(map);
    }

    static Guid IdOf(ModSubworld subworld)
    {
        string key = mainId.ToString("N") + "/" + subworld.FullName;
        byte[] bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), Hash(key, 0xCBF29CE484222325UL));
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 8), Hash(key, 0x84222325CBF29CE4UL));
        return new Guid(bytes);
    }

    static ulong Hash(string text, ulong seed)
    {
        ulong hash = seed;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 0x100000001B3UL;
        }

        return hash;
    }
}
