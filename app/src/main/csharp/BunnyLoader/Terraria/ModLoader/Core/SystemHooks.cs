using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.IO;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader.Core;

internal static unsafe class SystemHooks
{
    const int PlayersBegin = 0, PlayersEnd = 1, NPCsBegin = 2, NPCsEnd = 3, GoresEnd = 4, ItemsBegin = 5, ItemsEnd = 6, Transform = 7, ClockAdvance = 8;

    static double clockRate = 1, tileRemainder, eventRemainder;
    static readonly Dictionary<string, JsonObject> Headers = new(StringComparer.Ordinal);

    static bool Has(Type type, string hook) => ItemHooks.Overrides(type, typeof(ModSystem), hook);

    internal static void Install(Type type)
    {
        bool Any(params string[] hooks)
        {
            foreach (string hook in hooks)
                if (Has(type, hook)) return true;
            return false;
        }

        SystemDrawHooks.Install(name => Has(type, name));
        if (Has(type, nameof(ModSystem.ModifyGameTipVisibility))) GameTipHooks.Install();
        SystemNetHooks.Install(name => Has(type, name));
        if (Any(nameof(ModSystem.ModifyTransformMatrix))) Stage(Transform);
        if (Any(nameof(ModSystem.PreUpdatePlayers))) Stage(PlayersBegin);
        if (Any(nameof(ModSystem.PostUpdatePlayers))) Stage(PlayersEnd);
        if (Any(nameof(ModSystem.PreUpdateNPCs))) Stage(NPCsBegin);
        if (Any(nameof(ModSystem.PostUpdateNPCs), nameof(ModSystem.PreUpdateGores))) Stage(NPCsEnd);
        if (Any(nameof(ModSystem.PostUpdateGores))) Stage(GoresEnd);
        if (Any(nameof(ModSystem.PostUpdateProjectiles), nameof(ModSystem.PreUpdateItems))) Stage(ItemsBegin);
        if (Any(nameof(ModSystem.PostUpdateItems))) Stage(ItemsEnd);
        if (Any(nameof(ModSystem.PreUpdateEntities), nameof(ModSystem.PostUpdateEverything)))
            ItemHooks.Once("SystemUpdateEverything", () => On_Main.DoUpdateInWorld += DoUpdateInWorld);
        if (Any(nameof(ModSystem.PreUpdateProjectiles)))
            ItemHooks.Once("SystemProjectiles", () => On_Main.PreUpdateAllProjectiles += PreUpdateAllProjectiles);
        if (Any(nameof(ModSystem.PreUpdateDusts), nameof(ModSystem.PostUpdateDusts)))
            ItemHooks.Once("SystemDusts", () => On_Dust.UpdateDust += UpdateDust);
        if (Any(nameof(ModSystem.PreUpdateInvasions), nameof(ModSystem.PostUpdateInvasions)))
            ItemHooks.Once("SystemInvasions", () => On_Main.UpdateInvasion += UpdateInvasion);
        if (Any(nameof(ModSystem.PreUpdateTime), nameof(ModSystem.PostUpdateTime)))
            ItemHooks.Once("SystemTime", () => On_Main.UpdateTime += UpdateTime);
        if (Any(nameof(ModSystem.PreUpdateWorld), nameof(ModSystem.PostUpdateWorld)))
            ItemHooks.Once("SystemWorld", () => On_WorldGen.UpdateWorld += UpdateWorld);
        if (Any(nameof(ModSystem.UpdateUI))) ItemHooks.Once("SystemUI", () => On_Main.DoUpdate += DoUpdate);
        if (Any(nameof(ModSystem.PostUpdateInput))) ItemHooks.Once("SystemInput", () => On_Main.DoUpdate_HandleInput += HandleInput);
        if (Any(nameof(ModSystem.ModifyTimeRate)))
            ItemHooks.Once("SystemTimeRate", () =>
            {
                Stage(ClockAdvance);
                On_Main.UpdateTimeRate += UpdateTimeRate;
            });
        if (Any(nameof(ModSystem.ResetNearbyTileEffects), nameof(ModSystem.TileCountsAvailable)))
            ItemHooks.Once("SystemTileCounts", () =>
            {
                On_SceneMetrics.Reset += ResetMetrics;
                On_SceneMetrics.AggregateTileCounts += AggregateTileCounts;
                On_WorldGen.SaveAndQuit += ResetOnQuit;
                On_WorldGen.clearWorld += ResetOnClear;
            });
        if (Any(nameof(ModSystem.ClearWorld), nameof(ModSystem.OnWorldLoad)))
            ItemHooks.Once("SystemClearWorld", () => On_WorldGen.clearWorld += ClearWorld);
        if (Any(nameof(ModSystem.OnWorldLoad), nameof(ModSystem.LoadWorldData), nameof(ModSystem.CanWorldBePlayed)))
            ItemHooks.Once("SystemLoadWorld", () => On_WorldFile.LoadWorld += LoadWorld);
        if (Any(nameof(ModSystem.SaveWorldData), nameof(ModSystem.SaveWorldHeader)))
            ItemHooks.Once("SystemSaveWorld", () => On_WorldFile.InternalSaveWorld += InternalSaveWorld);
        if (Any(nameof(ModSystem.PreSaveAndQuit))) ItemHooks.Once("SystemPreSaveAndQuit", () => On_WorldGen.SaveAndQuit += SaveAndQuit);
        if (Any(nameof(ModSystem.OnWorldUnload))) ItemHooks.Once("SystemWorldUnload", () => On_WorldGen.SaveAndQuitCallBack += SaveAndQuitCallBack);
        if (Any(nameof(ModSystem.SaveWorldHeader), nameof(ModSystem.CanWorldBePlayed)))
            ItemHooks.Once("SystemHeaders", () => On_WorldFile.GetAllMetadata += GetAllMetadata);
        if (Any(nameof(ModSystem.CanWorldBePlayed)))
            ItemHooks.Once("SystemWorldPlayable", () =>
            {
                On_GUIWorldSelectMenu.CanWorldBePlayed += CanWorldBePlayed;
                On_GUIWorldSelectMenu.PlayWorldCheck += PlayWorldCheck;
            });
        if (Any(nameof(ModSystem.OnModUnload))) ItemHooks.Once("SystemModUnload", () => On_Main.QuitGame += QuitGame);
        if (Any(nameof(ModSystem.OnLocalizationsLoaded)))
            ItemHooks.Once("SystemLocalizations", () => Terraria.Localization.On_LanguageManager.SetLanguage_GameCulture += SetLanguage);
    }

    static void Stage(int stage) =>
        ItemHooks.Once($"SystemStage{stage}", () =>
        {
            if (Host.Native()->InstallSystemStage(stage, &OnStage) == 0) Log.Error($"ModSystem: update stage {stage} is unavailable on this game build");
        });

    [UnmanagedCallersOnly]
    static void OnStage(int stage, double* value)
    {
        try
        {
            switch (stage)
            {
                case PlayersBegin:
                    SystemLoader.PreUpdatePlayers();
                    break;
                case PlayersEnd:
                    SystemLoader.PostUpdatePlayers();
                    break;
                case NPCsBegin:
                    SystemLoader.PreUpdateNPCs();
                    break;
                case NPCsEnd:
                    SystemLoader.PostUpdateNPCs();
                    SystemLoader.PreUpdateGores();
                    break;
                case GoresEnd:
                    SystemLoader.PostUpdateGores();
                    break;
                case ItemsBegin:
                    SystemLoader.PostUpdateProjectiles();
                    SystemLoader.PreUpdateItems();
                    break;
                case ItemsEnd:
                    SystemLoader.PostUpdateItems();
                    break;
                case Transform:
                    SystemDrawHooks.ModifyTransformMatrix();
                    break;
                case ClockAdvance:
                    if (value != null) *value = clockRate;
                    break;
            }
        }
        catch (Exception e) { ItemHooks.Report("ModSystem update stage", e); }
    }

    static void DoUpdateInWorld(On_Main.orig_DoUpdateInWorld orig, Main self)
    {
        SystemLoader.PreUpdateEntities();
        orig(self);
        SystemLoader.PostUpdateEverything();
    }

    static void PreUpdateAllProjectiles(On_Main.orig_PreUpdateAllProjectiles orig, Main self)
    {
        SystemLoader.PreUpdateProjectiles();
        orig(self);
    }

    static void UpdateDust(On_Dust.orig_UpdateDust orig)
    {
        SystemLoader.PreUpdateDusts();
        orig();
        SystemLoader.PostUpdateDusts();
    }

    static void UpdateInvasion(On_Main.orig_UpdateInvasion orig)
    {
        SystemLoader.PreUpdateInvasions();
        orig();
        SystemLoader.PostUpdateInvasions();
    }

    static void UpdateTime(On_Main.orig_UpdateTime orig)
    {
        SystemLoader.PreUpdateTime();
        orig();
        SystemLoader.PostUpdateTime();
    }

    static void UpdateWorld(On_WorldGen.orig_UpdateWorld orig)
    {
        SystemLoader.PreUpdateWorld();
        orig();
        SystemLoader.PostUpdateWorld();
    }

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (!Main.gameMenu) SystemLoader.UpdateUI(gameTime);
    }

    static void HandleInput(On_Main.orig_DoUpdate_HandleInput orig, Main self)
    {
        orig(self);
        SystemLoader.PostUpdateInput();
    }

    static double Rate(double value, double fallback) => double.IsFinite(value) && value >= 0 && value <= int.MaxValue ? value : fallback;

    static void UpdateTimeRate(On_Main.orig_UpdateTimeRate orig)
    {
        orig();
        double time = Main.dayRate, tiles = Main.desiredWorldTilesUpdateRate, events = Main.dayRate;
        double initialTime = time, initialTiles = tiles;
        SystemLoader.ModifyTimeRate(ref time, ref tiles, ref events);
        clockRate = Rate(time, initialTime);
        double tileRate = Rate(tiles, initialTiles) + tileRemainder;
        double eventRate = Rate(events, initialTime) + eventRemainder;
        Main.desiredWorldTilesUpdateRate = (int)Math.Floor(tileRate);
        Main.dayRate = (int)Math.Floor(eventRate);
        tileRemainder = tileRate % 1;
        eventRemainder = eventRate % 1;
    }

    static bool IsPlayerScan(SceneMetrics self) => !Main.gameMenu && self == Main.PlayerSceneMetrics;

    static void ResetMetrics(On_SceneMetrics.orig_Reset orig, SceneMetrics self)
    {
        orig(self);
        if (IsPlayerScan(self)) SystemLoader.ResetNearbyTileEffects();
    }

    static void AggregateTileCounts(On_SceneMetrics.orig_AggregateTileCounts orig, SceneMetrics self)
    {
        orig(self);
        if (!IsPlayerScan(self)) return;
        GameArray<int> counts = self._tileCounts;
        if (!counts.IsNull && counts.Length >= TileLoader.TileCount) SystemLoader.TileCountsAvailable(counts.AsSpan());
    }

    static void ResetOnQuit(On_WorldGen.orig_SaveAndQuit orig)
    {
        SystemLoader.ResetNearbyTileEffects();
        orig();
    }

    static void ResetOnClear(On_WorldGen.orig_clearWorld orig)
    {
        orig();
        SystemLoader.ResetNearbyTileEffects();
    }

    static void ClearWorld(On_WorldGen.orig_clearWorld orig)
    {
        orig();
        SystemLoader.ClearWorld();
        if (Main.netMode == 1) SystemLoader.OnWorldLoad();
    }

    static string? Rejection(PlayerFileData player, WorldFileData world)
    {
        if (player.IsNull || world.IsNull || SystemLoader.CanWorldBePlayed(player, world, out ModSystem? rejector) || rejector == null) return null;
        try
        {
            string message = rejector.WorldCanBePlayedRejectionMessage(player, world);
            if (!string.IsNullOrWhiteSpace(message)) return message;
        }
        catch (Exception e) { ItemHooks.Report($"{rejector.FullName}.WorldCanBePlayedRejectionMessage", e); }
        return $"{rejector.Mod.DisplayName} does not allow this character in this world";
    }

    static void LoadWorld(On_WorldFile.orig_LoadWorld orig, bool canCheckFileState)
    {
        string? blocked = Rejection(Main.ActivePlayerFileData, Main.ActiveWorldFileData);
        if (blocked != null)
        {
            Log.Error($"ModSystem: {blocked}");
            return;
        }
        orig(canCheckFileState);
        SystemLoader.OnWorldLoad();
        LoadData();
        SystemLoader.PostWorldLoad();
    }

    static string? DataFile() => Main.worldPathName is { Length: > 0 } path ? path + ".bl.json" : null;

    static string HeaderFile(string worldPath) => worldPath + ".bl.header.json";

    internal static JsonObject ReadObject(string? file)
    {
        try
        {
            if (file == null || !File.Exists(file)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject();
        }
        catch (Exception e)
        {
            Log.Error($"ModSystem: {file} could not be read ({e.Message}); its data is ignored");
            return new JsonObject();
        }
    }

    internal static void WriteObject(string file, JsonObject all)
    {
        try
        {
            if (all.Count == 0)
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            string temporary = file + ".tmp";
            File.WriteAllText(temporary, all.ToJsonString());
            File.Move(temporary, file, true);
        }
        catch (Exception e) { Log.Error($"ModSystem: {file} could not be written: {e.Message}"); }
    }

    internal static TagCompound TagOf(JsonNode? node)
    {
        if (node is not JsonObject) return new TagCompound();
        try { return TagJson.Read(node.ToJsonString()); }
        catch (Exception e)
        {
            Log.Error($"ModSystem: saved data could not be read ({e.Message})");
            return new TagCompound();
        }
    }

    static void LoadData()
    {
        ModSystem[] systems = SystemLoader.With(nameof(ModSystem.LoadWorldData));
        if (systems.Length == 0) return;
        JsonObject all = ReadObject(DataFile());
        foreach (ModSystem system in systems) SystemLoader.LoadWorldData(system, TagOf(all[SystemLoader.KeyOf(system)]));
    }

    static void Store(JsonObject all, string key, TagCompound tag)
    {
        if (tag.Count == 0) all.Remove(key);
        else all[key] = JsonNode.Parse(TagJson.Write(tag));
    }

    static void InternalSaveWorld(On_WorldFile.orig_InternalSaveWorld orig, bool useCloudSaving, bool resetTime)
    {
        orig(useCloudSaving, resetTime);
        if (useCloudSaving || Main.worldPathName is not { Length: > 0 } path) return;
        ModSystem[] data = SystemLoader.With(nameof(ModSystem.SaveWorldData));
        if (data.Length > 0)
        {
            string file = path + ".bl.json";
            JsonObject all = ReadObject(file);
            foreach (ModSystem system in data)
            {
                var tag = new TagCompound();
                SystemLoader.SaveWorldData(system, tag);
                Store(all, SystemLoader.KeyOf(system), tag);
            }
            WriteObject(file, all);
        }
        ModSystem[] headers = SystemLoader.With(nameof(ModSystem.SaveWorldHeader));
        if (headers.Length == 0) return;
        string headerFile = HeaderFile(path);
        JsonObject saved = ReadObject(headerFile);
        foreach (ModSystem system in headers)
        {
            var tag = new TagCompound();
            SystemLoader.SaveWorldHeader(system, tag);
            Store(saved, SystemLoader.KeyOf(system), tag);
        }
        WriteObject(headerFile, saved);
        lock (Headers) Headers[path] = saved;
    }

    static void SaveAndQuit(On_WorldGen.orig_SaveAndQuit orig)
    {
        SystemLoader.PreSaveAndQuit();
        orig();
    }

    static void SaveAndQuitCallBack(On_WorldGen.orig_SaveAndQuitCallBack orig, GameObject threadContext)
    {
        orig(threadContext);
        SystemLoader.OnWorldUnload();
    }

    static WorldFileData GetAllMetadata(On_WorldFile.orig_GetAllMetadata orig, string? file, bool cloudSave)
    {
        WorldFileData data = orig(file, cloudSave);
        if (!data.IsNull && !cloudSave && file != null)
        {
            JsonObject saved = ReadObject(HeaderFile(file));
            lock (Headers) Headers[file] = saved;
        }
        return data;
    }

    internal static bool TryGetHeaderData(WorldFileData world, ModSystem system, out TagCompound data)
    {
        data = new TagCompound();
        if (world.IsNull || world.IsCloudSave || world.Path is not { Length: > 0 } path) return false;
        JsonObject? saved;
        lock (Headers)
        {
            if (!Headers.TryGetValue(path, out saved))
            {
                saved = ReadObject(HeaderFile(path));
                Headers[path] = saved;
            }
        }
        if (saved[SystemLoader.KeyOf(system)] is not JsonObject node) return false;
        data = TagOf(node);
        return true;
    }

    static bool CanWorldBePlayed(On_GUIWorldSelectMenu.orig_CanWorldBePlayed orig, GUIWorldSelectMenu self, WorldFileData world) =>
        orig(self, world) && Rejection(Main.ActivePlayerFileData, world) == null;

    static void PlayWorldCheck(On_GUIWorldSelectMenu.orig_PlayWorldCheck orig, GUIWorldSelectMenu self)
    {
        var worlds = new GameList(self.SortedWorldData);
        int index = self.SelectedItem;
        if (!worlds.IsNull && index >= 0 && index < worlds.Count)
        {
            string? blocked = Rejection(Main.ActivePlayerFileData, new WorldFileData(worlds.Get<IntPtr>(index)));
            if (blocked != null)
            {
                Log.Error($"ModSystem: {blocked}");
                return;
            }
        }
        orig(self);
    }

    static void SetLanguage(Terraria.Localization.On_LanguageManager.orig_SetLanguage_GameCulture orig, Terraria.Localization.LanguageManager self,
        Terraria.Localization.GameCulture culture)
    {
        orig(self, culture);
        if (SystemLoader.Localized) SystemLoader.OnLocalizationsLoaded();
    }

    static void QuitGame(On_Main.orig_QuitGame orig, Main self)
    {
        SystemLoader.OnModUnload();
        orig(self);
    }
}
