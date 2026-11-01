using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BunnyLoader.Interop;
using Terraria.GameContent.Personalities;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class BiomeLoader
{
    static readonly List<ModBiome> Biomes = new();
    static readonly bool[][] Flags = new bool[256][];
    static readonly Dictionary<nint, ModBiome> ByShopping = new();
    static readonly List<GameHandle> ShoppingRoots = new();
    static AShoppingBiome[] shopping = Array.Empty<AShoppingBiome>();

    public static int Count => Biomes.Count;

    internal static IReadOnlyList<ModBiome> All => Biomes;

    internal static int Register(ModBiome biome)
    {
        Biomes.Add(biome);
        BiomeHooks.Install();
        return Biomes.Count - 1;
    }

    internal static void SetupContent(ModBiome biome)
    {
        string dialogue = Language.GetOrRegister(biome.ShoppingNameKey, () => "the " + Regex.Replace(biome.Name, "([A-Z])", " $1").Trim()).Value ?? "";
        Language.GetOrRegister("TownNPCMoodBiomes." + biome.ShoppingNameKey, () => dialogue);
        _ = biome.DisplayName;
    }

    internal static bool[] FlagsOf(int who)
    {
        bool[]? flags = Flags[who];
        if (flags == null || flags.Length != Biomes.Count)
        {
            var grown = new bool[Biomes.Count];
            if (flags != null) Array.Copy(flags, grown, Math.Min(flags.Length, grown.Length));
            Flags[who] = flags = grown;
        }
        return flags;
    }

    internal static bool InBiome(Player player, ModBiome biome)
    {
        int who = player.whoAmI;
        if ((uint)who >= (uint)Flags.Length || Flags[who] is not { } flags) return false;
        int index = biome.ZeroIndexType;
        return (uint)index < (uint)flags.Length && flags[index];
    }

    public static void UpdateBiomes(Player player)
    {
        bool[] flags = FlagsOf(player.whoAmI);
        bool changed = false;
        for (int i = 0; i < Biomes.Count; i++)
        {
            ModBiome biome = Biomes[i];
            bool before = flags[i];
            bool now;
            try { now = biome.IsBiomeActive(player); }
            catch (Exception e)
            {
                ItemHooks.Report($"{biome.FullName}.IsBiomeActive", e);
                now = false;
            }
            flags[i] = now;
            if (before != now) changed = true;
            try
            {
                if (!before && now) biome.OnEnter(player);
                else if (before && !now) biome.OnLeave(player);
                if (now) biome.OnInBiome(player);
            }
            catch (Exception e) { ItemHooks.Report($"{biome.FullName} (enter, in, leave)", e); }
        }
        if (changed) BiomeHooks.Changed(player);
    }

    internal static void LeaveWorld()
    {
        int local = Main.myPlayer;
        for (int who = 0; who < Flags.Length; who++)
        {
            bool[]? flags = Flags[who];
            if (flags == null) continue;
            for (int i = 0; i < flags.Length; i++)
            {
                if (!flags[i]) continue;
                flags[i] = false;
                if (who != local) continue;
                ModBiome biome = Biomes[i];
                try { biome.OnLeave(Main.player[who]); }
                catch (Exception e) { ItemHooks.Report($"{biome.FullName}.OnLeave", e); }
            }
            SceneEffectLoader.Clear(who);
        }
    }

    internal static ModBiome? ByKey(string key)
    {
        foreach (ModBiome biome in Biomes)
            if (KeyOf(biome) == key) return biome;
        return null;
    }

    internal static string KeyOf(ModBiome biome) => $"{biome.Mod.Uid}/{biome.Name}";

    internal static ModBiome? ByType(Type type)
    {
        foreach (ModBiome biome in Biomes)
            if (biome.GetType() == type) return biome;
        return null;
    }

    internal static AShoppingBiome ShoppingOf(ModBiome biome)
    {
        if (shopping.Length != Biomes.Count) Array.Resize(ref shopping, Biomes.Count);
        AShoppingBiome existing = shopping[biome.Type];
        if (!existing.IsNull) return existing;
        MushroomBiome proxy = MushroomBiome.New();
        ShoppingRoots.Add(new GameHandle(proxy.Pointer));
        proxy.NameKey = biome.ShoppingNameKey;
        ByShopping[proxy.Pointer] = biome;
        BiomeHooks.InstallShopping();
        return shopping[biome.Type] = new AShoppingBiome(proxy.Pointer);
    }

    internal static ModBiome? OfShopping(nint pointer) => ByShopping.TryGetValue(pointer, out ModBiome? biome) ? biome : null;
}
