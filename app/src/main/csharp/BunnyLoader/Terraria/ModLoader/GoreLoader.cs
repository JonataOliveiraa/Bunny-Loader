using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class GoreLoader
{
    static readonly object Gate = new();
    static readonly List<ModGore> pending = new();
    static readonly Dictionary<int, ModGore> gores = new();

    public static int GoreCount => TextureAssets.Gore_.IsNull ? GoreID.Count : TextureAssets.Gore_.Length;

    public static bool AddGoreFromTexture<TGore>(Mod mod, string texture) where TGore : ModGore, new()
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        return mod.AddContent(new TGore
        {
            nameOverride = Path.GetFileNameWithoutExtension(texture),
            textureOverride = texture,
        });
    }

    internal static void RegisterModGore(ModGore gore)
    {
        lock (Gate) pending.Add(gore);
        GoreHooks.Install(gore.GetType());
    }

    internal static void AutoloadGores(Mod mod)
    {
        foreach (string file in mod.GetFileNames())
        {
            if (!file.Contains("Gores/") || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            string texturePath = Path.ChangeExtension(file, null);
            if (mod.TryFind<ModGore>(Path.GetFileName(texturePath), out _)) continue;
            AddGoreFromTexture<SimpleModGore>(mod, $"{mod.Name}/{texturePath}");
        }
    }

    internal static ModGore? GetModGore(int type)
    {
        lock (Gate) return gores.TryGetValue(type, out ModGore? gore) ? gore : null;
    }

    internal static void ResizeArrays()
    {
        ModGore[] adding;
        lock (Gate)
        {
            adding = pending.ToArray();
            pending.Clear();
        }
        if (adding.Length == 0) return;
        int first = GoreCount, total = first + adding.Length;
        TextureAssets.ResizeGore(total);
        Grow(ref GoreID.Sets.SpecialAI, total);
        Grow(ref GoreID.Sets.DisappearSpeed, total);
        Grow(ref GoreID.Sets.DisappearSpeedAlpha, total);
        Grow(ref GoreID.Sets.IsDrip, total);
        Grow(ref ChildSafety.SafeGore, total);
        for (int i = 0; i < adding.Length; i++)
        {
            ModGore gore = adding[i];
            int type = first + i;
            gore.Type = type;
            lock (Gate) gores[type] = gore;
            GoreID.Sets.DisappearSpeed[type] = 1;
            GoreID.Sets.DisappearSpeedAlpha[type] = 1;
            try { TextureAssets.Gore[type] = new Asset<Texture2D>(gore.Texture, ItemLoader.ResolveTexture(gore, gore.Texture)); }
            catch (Exception e) { Log.Error($"gore {gore.FullName}: the texture could not be loaded: {e.Message}"); }
        }
        Log.Info($"mod gores: {adding.Length} (types {first}..{total - 1})");
    }

    static void Grow<T>(ref GameArray<T> set, int total) where T : unmanaged
    {
        if (!set.IsNull && set.Length < total) set = set.Resized(total);
    }
}
