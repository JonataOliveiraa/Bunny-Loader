using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsModSystem;

public sealed class TestSystem : ModSystem
{
    public static int ModLoads, Resizes, Localizations, SetupRecipes, WorldLoads, Clears, CanPlay, Resets, Counts, LastCountsLength, UIs;
    public static int LoadedCount = -1;
    public static bool LoadedBeforeData, HadData;
    public static double TimeRate = 1;
    public static bool Recording;
    public static readonly List<string> Trace = new();
    public static readonly Dictionary<string, int> Calls = new();
    public static int SavedCount;

    static bool recordingFrame;

    static void Mark(string hook)
    {
        Calls[hook] = Calls.TryGetValue(hook, out int n) ? n + 1 : 1;
        if (Recording && hook == nameof(PreUpdateEntities)) recordingFrame = true;
        if (!recordingFrame) return;
        Trace.Add(hook);
        if (hook != nameof(PostUpdateEverything)) return;
        recordingFrame = false;
        Recording = false;
    }

    public override void OnModLoad() => ModLoads++;

    public override void ResizeArrays() => Resizes++;

    public override void OnLocalizationsLoaded() => Localizations++;

    public override void PostSetupRecipes() => SetupRecipes++;

    public override void OnWorldLoad()
    {
        WorldLoads++;
        LoadedBeforeData = LoadedCount < 0;
    }

    public override void ClearWorld() => Clears++;

    public override bool CanWorldBePlayed(PlayerFileData playerData, WorldFileData worldFileData)
    {
        CanPlay++;
        return true;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (SavedCount > 0) tag["count"] = SavedCount;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        HadData = tag.ContainsKey("count");
        LoadedCount = tag.GetInt("count");
    }

    public override void SaveWorldHeader(TagCompound tag) => tag["marker"] = 9;

    public override void PreUpdateEntities() => Mark(nameof(PreUpdateEntities));

    public override void PreUpdatePlayers() => Mark(nameof(PreUpdatePlayers));

    public override void PostUpdatePlayers() => Mark(nameof(PostUpdatePlayers));

    public override void PreUpdateNPCs() => Mark(nameof(PreUpdateNPCs));

    public override void PostUpdateNPCs() => Mark(nameof(PostUpdateNPCs));

    public override void PreUpdateGores() => Mark(nameof(PreUpdateGores));

    public override void PostUpdateGores() => Mark(nameof(PostUpdateGores));

    public override void PreUpdateProjectiles() => Mark(nameof(PreUpdateProjectiles));

    public override void PostUpdateProjectiles() => Mark(nameof(PostUpdateProjectiles));

    public override void PreUpdateItems() => Mark(nameof(PreUpdateItems));

    public override void PostUpdateItems() => Mark(nameof(PostUpdateItems));

    public override void PreUpdateDusts() => Mark(nameof(PreUpdateDusts));

    public override void PostUpdateDusts() => Mark(nameof(PostUpdateDusts));

    public override void PreUpdateTime() => Mark(nameof(PreUpdateTime));

    public override void PostUpdateTime() => Mark(nameof(PostUpdateTime));

    public override void PreUpdateWorld() => Mark(nameof(PreUpdateWorld));

    public override void PostUpdateWorld() => Mark(nameof(PostUpdateWorld));

    public override void PreUpdateInvasions() => Mark(nameof(PreUpdateInvasions));

    public override void PostUpdateInvasions() => Mark(nameof(PostUpdateInvasions));

    public override void PostUpdateEverything() => Mark(nameof(PostUpdateEverything));

    public override void PostUpdateInput() => Mark(nameof(PostUpdateInput));

    public override void UpdateUI(GameTime gameTime)
    {
        if (gameTime.IsNull) return;
        UIs++;
    }

    public override void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate) => timeRate *= TimeRate;

    public override void ResetNearbyTileEffects() => Resets++;

    public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
    {
        Counts++;
        LastCountsLength = tileCounts.Length;
    }
}
