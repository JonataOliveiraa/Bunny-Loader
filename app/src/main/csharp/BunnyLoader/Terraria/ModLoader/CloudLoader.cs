using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class CloudLoader
{
    static readonly List<ModCloud> pending = new();
    internal static readonly Dictionary<int, ModCloud> clouds = new();

    public static int CloudCount { get; private set; } = -1;

    public static bool cloudLoaded;

    public static bool AddCloudFromTexture<TCloud>(Mod mod, string texture, float spawnChance = 1f, bool rareCloud = false) where TCloud : ModCloud, new()
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        if (!mod.loading) throw new InvalidOperationException($"{mod.Name}: AddCloudFromTexture can only be called while the mod loads");
        return mod.AddContent(new TCloud
        {
            nameOverride = Path.GetFileNameWithoutExtension(texture),
            textureOverride = texture,
            spawnChance = spawnChance,
            rareCloud = rareCloud,
        });
    }

    public static bool AddCloudFromTexture(Mod mod, string texture, float spawnChance = 1f, bool rareCloud = false) =>
        AddCloudFromTexture<SimpleModCloud>(mod, texture, spawnChance, rareCloud);

    internal static void RegisterModCloud(ModCloud cloud)
    {
        lock (pending) pending.Add(cloud);
        CloudHooks.Install(cloud.GetType());
    }

    internal static void AutoloadClouds(Mod mod)
    {
        foreach (string file in mod.GetFileNames())
        {
            if (!file.Contains("Clouds/") || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            string texturePath = Path.ChangeExtension(file, null);
            if (mod.TryFind<ModCloud>(Path.GetFileName(texturePath), out _)) continue;
            AddCloudFromTexture<SimpleModCloud>(mod, $"{mod.Name}/{texturePath}");
        }
    }

    internal static ModCloud? GetModCloud(int type) => clouds.TryGetValue(type, out ModCloud? cloud) ? cloud : null;

    internal static IEnumerable<ModCloud> All => clouds.Values;

    public static int? ChooseCloud(float vanillaPool, bool rare)
    {
        if (!cloudLoaded) return 0;
        float total = Math.Max(0, vanillaPool);
        List<(int Type, float Weight)>? pool = null;
        foreach (ModCloud cloud in clouds.Values)
        {
            if (rare != cloud.RareCloud) continue;
            float weight;
            try { weight = cloud.SpawnChance(); }
            catch (Exception e)
            {
                ItemHooks.Report($"{cloud.FullName}.SpawnChance", e);
                continue;
            }
            if (!(weight > 0f)) continue;
            (pool ??= new()).Add((cloud.Type, weight));
            total += weight;
        }
        if (pool == null) return 0;
        float choice = (float)Main.rand.NextDouble() * total;
        if (choice < vanillaPool) return 0;
        choice -= Math.Max(0, vanillaPool);
        foreach ((int type, float weight) in pool)
        {
            if (choice < weight) return type;
            choice -= weight;
        }
        return null;
    }

    internal static void ResizeArrays()
    {
        ModCloud[] adding;
        lock (pending)
        {
            adding = pending.ToArray();
            pending.Clear();
        }
        if (adding.Length == 0) return;
        int first = CloudID.Count + clouds.Count, total = first + adding.Length;
        CloudCount = total;
        WaterStylesLoader.Grow(ref TextureAssets.Cloud_, total);
        for (int i = 0; i < adding.Length; i++)
        {
            ModCloud cloud = adding[i];
            cloud.Type = first + i;
            try
            {
                var asset = new Asset<Texture2D>(cloud.Texture, ItemLoader.ResolveTexture(cloud, cloud.Texture));
                asset.Wait();
                TextureAssets.Cloud_[cloud.Type] = asset;
                clouds[cloud.Type] = cloud;
            }
            catch (Exception e) { Log.Error($"cloud {cloud.FullName}: the texture could not be loaded: {e.Message}"); }
        }
        GrowMasks(total, adding);
        cloudLoaded = clouds.Count > 0;
        Log.Info($"mod clouds: {adding.Length} (types {first}..{total - 1})");
    }

    static void GrowMasks(int total, ModCloud[] adding)
    {
        GameArray<TextureMask> masks = TextureMaskManager.CloudMasks;
        if (masks.IsNull) return;
        if (masks.Length < total) TextureMaskManager.CloudMasks = masks = masks.Resized(total);
        foreach (ModCloud cloud in adding)
        {
            if (masks[cloud.Type].Pointer != IntPtr.Zero) continue;
            TextureMask mask = TextureMask.New();
            if (clouds.ContainsKey(cloud.Type))
            {
                Texture2D texture = TextureAssets.Cloud[cloud.Type].Value;
                mask.srcWidth = texture.Width;
                mask.srcHeight = texture.Height;
            }
            masks[cloud.Type] = mask;
        }
    }
}
