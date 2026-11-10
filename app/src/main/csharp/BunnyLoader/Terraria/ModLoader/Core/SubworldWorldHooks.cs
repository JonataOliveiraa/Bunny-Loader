using System;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.IO;
using Terraria.Utilities;

namespace Terraria.ModLoader.Core;

internal static unsafe class SubworldWorldHooks
{
    static ModSubworld? active;
    static bool hideUnderworld, randomTileUpdates, savePlayerChanges = true, patched;

    internal static void Install()
    {
        On_WorldGen.UpdateWorld += UpdateWorld;
        On_Main.DrawUnderworldBackground += DrawUnderworldBackground;
        On_Player.SavePlayer += SavePlayer;
        On_Main.EraseWorld += EraseWorld;
    }

    internal static bool SavesPlayer => active == null || savePlayerChanges;

    internal static void Apply(ModSubworld? subworld)
    {
        active = subworld;
        hideUnderworld = subworld != null && Read(subworld, nameof(ModSubworld.HideUnderworld), () => subworld.HideUnderworld, true);
        randomTileUpdates = subworld != null && Read(subworld, nameof(ModSubworld.RandomTileUpdates), () => subworld.RandomTileUpdates, false);
        savePlayerChanges = subworld == null || Read(subworld, nameof(ModSubworld.SavePlayerChanges), () => subworld.SavePlayerChanges, true);
        if (hideUnderworld == patched) return;
        if (Host.Native()->HideUnderworld(hideUnderworld ? 1 : 0) != 0) patched = hideUnderworld;
    }

    static bool Read(ModSubworld subworld, string name, Func<bool> value, bool fallback)
    {
        try { return value(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{subworld.FullName}.{name}", e);
            return fallback;
        }
    }

    static void UpdateWorld(On_WorldGen.orig_UpdateWorld orig)
    {
        ModSubworld? subworld = active;
        if (subworld == null)
        {
            orig();
            return;
        }

        int rate = Main.desiredWorldTilesUpdateRate;
        if (!randomTileUpdates) Main.desiredWorldTilesUpdateRate = 0;
        try { orig(); }
        finally { Main.desiredWorldTilesUpdateRate = rate; }
        try { subworld.Update(); }
        catch (Exception e) { ItemHooks.Report(subworld.FullName + ".Update", e); }
    }

    static void DrawUnderworldBackground(On_Main.orig_DrawUnderworldBackground orig, Main self, bool flat)
    {
        if (!hideUnderworld) orig(self, flat);
    }

    static void SavePlayer(On_Player.orig_SavePlayer orig, PlayerFileData playerFile, bool skipMapSave, bool forceSave)
    {
        if (SavesPlayer) orig(playerFile, skipMapSave, forceSave);
    }

    internal static void ReloadPlayer()
    {
        PlayerFileData active = Main.ActivePlayerFileData;
        if (active.IsNull || string.IsNullOrEmpty(active.Path)) return;
        PlayerFileData fresh = Player.GetFileData(active.Path, active.IsCloudSave);
        Player player = fresh.IsNull ? default : fresh.Player;
        if (player.IsNull)
        {
            Log.Warn($"Subworlds: {active.Path} could not be read back; the changes made in the subworld stay");
            return;
        }

        player.whoAmI = Main.myPlayer;
        fresh.SetAsActive();
    }

    static void EraseWorld(On_Main.orig_EraseWorld orig, int i)
    {
        string? folder = null;
        var worlds = new GameList(Main.WorldList);
        if (!worlds.IsNull && i >= 0 && i < worlds.Count)
        {
            var world = new WorldFileData(worlds.Get<IntPtr>(i));
            if (!world.IsNull && !world.IsCloudSave) folder = Path.Combine(Main.WorldPath ?? "", world.UniqueId.ToString());
        }

        orig(i);
        if (folder != null) DeleteFolder(folder);
    }

    static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        try
        {
            foreach (string file in Directory.GetFiles(folder))
                if (FileUtilities.Exists(file, false))
                    FileUtilities.Delete(file, false);
            Directory.Delete(folder, true);
        }
        catch (Exception e) { Log.Warn($"Subworlds: {folder} could not be deleted ({e.Message})"); }
    }
}
