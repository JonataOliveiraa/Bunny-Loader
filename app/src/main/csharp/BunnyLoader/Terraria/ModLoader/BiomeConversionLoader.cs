using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class ModBiomeConversion : ModType
{
    public int Type { get; internal set; }

    protected sealed override void Register() => Type = BiomeConversionLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void PostSetupContent()
    {
    }
}

public static class BiomeConversionLoader
{
    internal static int BiomeConversionCount { get; private set; } = BiomeConversionID.Count;

    static readonly List<ModBiomeConversion> conversions = new();

    public static ModBiomeConversion? GetBiomeConversion(int type) =>
        type >= BiomeConversionID.Count && type < BiomeConversionCount ? conversions[type - BiomeConversionID.Count] : null;

    public static int Register(ModBiomeConversion conversion)
    {
        int type = BiomeConversionCount++;
        conversions.Add(conversion);
        TileConversionHooks.Install();
        return type;
    }

    internal static void PostSetupContent()
    {
        foreach (ModBiomeConversion conversion in conversions)
        {
            try
            {
                conversion.PostSetupContent();
            }
            catch (Exception e)
            {
                ItemHooks.Report(conversion.FullName + ".PostSetupContent", e);
            }
        }
    }
}

internal sealed class ConversionTable
{
    readonly Dictionary<int, Dictionary<int, List<Func<int, int, int, int, bool>>>> handlers = new();

    readonly Dictionary<int, int[]> fallbacks = new();

    internal void Add(int type, int conversionType, Func<int, int, int, int, bool> handler)
    {
        TileConversionHooks.Install();
        if (!handlers.TryGetValue(type, out var byConversion))
            handlers[type] = byConversion = new();
        if (!byConversion.TryGetValue(conversionType, out var list))
            byConversion[conversionType] = list = new();
        list.Add(handler);
    }

    internal bool Run(int i, int j, int type, int conversionType)
    {
        if (!handlers.TryGetValue(type, out var byConversion) || !byConversion.TryGetValue(conversionType, out var list))
            return true;
        foreach (var handler in list)
        {
            if (!handler(i, j, type, conversionType))
                return false;
        }

        return true;
    }

    int[] FallbacksOf(int type)
    {
        if (fallbacks.TryGetValue(type, out int[]? found))
        {
            if (found.Length >= BiomeConversionLoader.BiomeConversionCount)
                return found;
            int old = found.Length;
            Array.Resize(ref found, BiomeConversionLoader.BiomeConversionCount);
            Array.Fill(found, -1, old, found.Length - old);
            return fallbacks[type] = found;
        }

        found = new int[BiomeConversionLoader.BiomeConversionCount];
        Array.Fill(found, -1);
        return fallbacks[type] = found;
    }

    internal void AddFallback(int type, int fallback, int[] exceptions)
    {
        int[] table = FallbacksOf(type);
        int[] backup = (int[])table.Clone();
        Array.Fill(table, fallback);
        foreach (int conversion in exceptions)
            table[conversion] = backup[conversion];
    }

    internal void SetFallback(int type, int conversionType, int fallback) => FallbacksOf(type)[conversionType] = fallback;

    internal bool TryGetFallback(int type, int conversionType, out int fallback)
    {
        fallback = fallbacks.TryGetValue(type, out int[]? table) && conversionType < table.Length ? table[conversionType] : -1;
        return fallback >= 0;
    }
}

public static partial class TileLoader
{
    public delegate bool ConvertTile(int i, int j, int type, int conversionType);

    internal static readonly ConversionTable Conversions = new();

    public static void RegisterConversion(int tileType, int conversionType, ConvertTile conversionDelegate) =>
        Conversions.Add(tileType, conversionType, (i, j, type, conversion) => conversionDelegate(i, j, type, conversion));

    public static void RegisterConversion(int tileType, int conversionType, int toType) =>
        RegisterConversion(tileType, conversionType, (i, j, type, conversion) =>
        {
            WorldGen.ConvertTile(i, j, toType);
            return false;
        });

    public static void RegisterSimpleConversion(int tileType, int conversionType, int toType, bool purification = true)
    {
        RegisterConversion(tileType, conversionType, toType);
        RegisterConversionFallback(toType, tileType, conversionType);
        if (!purification)
            return;
        RegisterConversion(toType, BiomeConversionID.Purity, tileType);
        RegisterConversion(toType, BiomeConversionID.PurificationPowder, tileType);
        if (conversionType != BiomeConversionID.Hallow)
            RegisterConversion(toType, BiomeConversionID.ChlorophyteSpread, tileType);
    }

    public static void RegisterConversionFallback(int tileType, int fallbackType, params int[] exceptForConversionTypes) =>
        Conversions.AddFallback(tileType, fallbackType, exceptForConversionTypes);

    public static void SetConversionFallback(int tileType, int conversionType, int fallbackType) =>
        Conversions.SetFallback(tileType, conversionType, fallbackType);

    public static bool TryGetConversionFallback(int tileType, int conversionType, out int fallbackType) =>
        Conversions.TryGetFallback(tileType, conversionType, out fallbackType);

    public static bool Convert(int i, int j, int conversionType)
    {
        using var depth = new TileConversionHooks.Depth();
        Tile tile = Main.tile[i, j];
        int type = tile.type;
        if (!Conversions.Run(i, j, type, conversionType))
            return false;
        GetTile(type)?.Convert(i, j, conversionType);
        if (tile.type == type && TryGetConversionFallback(type, conversionType, out int fallback))
        {
            tile.type = (ushort)fallback;
            WorldGen.Convert(i, j, conversionType, walls: false);
            if (tile.type == fallback)
                tile.type = (ushort)type;
            return false;
        }

        return tile.type == type;
    }

    internal static void OnTileConverted(int i, int j, int fromType, int toType, int conversionType)
    {
        foreach (GlobalTile global in globals)
            global.OnTileConverted(i, j, fromType, toType, conversionType);
    }
}

public static unsafe partial class WallLoader
{
    public delegate bool ConvertWall(int i, int j, int type, int conversionType);

    internal static readonly ConversionTable Conversions = new();

    public static void RegisterConversion(int wallType, int conversionType, ConvertWall conversionDelegate) =>
        Conversions.Add(wallType, conversionType, (i, j, type, conversion) => conversionDelegate(i, j, type, conversion));

    public static void RegisterConversion(int wallType, int conversionType, int toType) =>
        RegisterConversion(wallType, conversionType, (i, j, type, conversion) =>
        {
            WorldGen.ConvertWall(i, j, toType);
            return false;
        });

    public static void RegisterSimpleConversion(int wallType, int conversionType, int toType, bool purification = true)
    {
        RegisterConversion(wallType, conversionType, toType);
        RegisterConversionFallback(toType, wallType, conversionType);
        if (!purification)
            return;
        RegisterConversion(toType, BiomeConversionID.Purity, wallType);
        RegisterConversion(toType, BiomeConversionID.PurificationPowder, wallType);
    }

    public static void RegisterConversionFallback(int wallType, int fallbackType, params int[] exceptForConversionTypes) =>
        Conversions.AddFallback(wallType, fallbackType, exceptForConversionTypes);

    public static void SetConversionFallback(int wallType, int conversionType, int fallbackType) =>
        Conversions.SetFallback(wallType, conversionType, fallbackType);

    public static bool TryGetConversionFallback(int wallType, int conversionType, out int fallbackType) =>
        Conversions.TryGetFallback(wallType, conversionType, out fallbackType);

    public static bool Convert(int i, int j, int conversionType)
    {
        using var depth = new TileConversionHooks.Depth();
        Tile tile = Main.tile[i, j];
        int type = tile.wall;
        if (!Conversions.Run(i, j, type, conversionType))
            return false;
        GetWall(type)?.Convert(i, j, conversionType);
        if (tile.wall == type && TryGetConversionFallback(type, conversionType, out int fallback))
        {
            tile.wall = (ushort)fallback;
            WorldGen.Convert(i, j, conversionType, tiles: false);
            if (tile.wall == fallback)
                tile.wall = (ushort)type;
            return false;
        }

        return tile.wall == type;
    }
}
