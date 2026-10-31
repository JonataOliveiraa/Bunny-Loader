using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;

namespace Terraria.ModLoader;

public static unsafe partial class TileLoader
{
    static uint[] methods = new uint[TileID.Count];

    static ModTile?[] tiles = new ModTile?[TileID.Count];

    static readonly List<GlobalTile> globals = new();

    static readonly Dictionary<(int, int), int> drops = new();

    static readonly List<ModTile> animated = new();

    static bool subscribed, ready;

    internal static readonly string[][] Events =
    {
        new[]
        {
            "KillTile"
        },
        new[]
        {
            "CanKillTile"
        },
        new[]
        {
            "CanPlace"
        },
        new[]
        {
            "PlaceInWorld"
        },
        new[]
        {
            "NumDust"
        },
        new[]
        {
            "CreateDust"
        },
        new[]
        {
            "CanDrop",
            "Drop",
            "GetItemDrops"
        },
        new[]
        {
            "HitWire",
            "PreHitWire"
        },
        new[]
        {
            "RandomUpdate"
        },
        new[]
        {
            "NearbyEffects"
        },
        new[]
        {
            "ModifyLight"
        },
        new[]
        {
            "AnimateTile"
        },
        new[]
        {
            "SetSpriteEffects",
            "SetDrawPositions",
            "AnimateIndividualTile"
        },
        new[]
        {
            "PreDraw",
            "DrawEffects"
        },
        new[]
        {
            "RightClick"
        },
        new[]
        {
            "MouseOver"
        },
        new[]
        {
            "MouseOverFar"
        },
        new[]
        {
            "TileFrame"
        },
        new[]
        {
            "Slope"
        },
        new[]
        {
            "IsTileDangerous"
        },
        new[]
        {
            "IsTileSpelunkable"
        },
        new[]
        {
            "CanReplace"
        },
        new[]
        {
            "KillSound"
        },
        new[]
        {
            "PostDraw",
            "DrawEffects",
            "SpecialDraw"
        },
        Array.Empty<string>(),
        new[]
        {
            "CanExplode"
        },
        new[]
        {
            "AdjTiles"
        }
    };

    public static int TileCount => tiles.Length;

    public static ModTile? GetTile(int type) => (uint)type < (uint)tiles.Length ? tiles[type] : null;

    internal static ModTile? At(int i, int j) => GetTile(Main.tile[i, j].type);

    internal static bool Has(ModTile tile, int bit) => (methods[tile.Type] & (1u << bit)) != 0;

    internal static bool Overrides(ModType value, string name)
    {
        foreach (var method in value.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (method.Name != name)
                continue;
            Type? root = method.GetBaseDefinition().DeclaringType;
            if (method.DeclaringType != root && (root == typeof(ModTile) || root == typeof(ModBlockType) || root == typeof(GlobalTile) || root == typeof(ModType)))
                return true;
        }

        return false;
    }

    internal static void Register(ModTile tile)
    {
        byte* mod = Host.Utf8(tile.Mod.Uid), name = Host.Utf8(tile.Name), texture = Host.Utf8(ItemLoader.ResolveTexture(tile, tile.Texture));
        int type;
        try
        {
            type = Host.Native()->RegisterTile(mod, name, texture);
        }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(texture);
        }

        if (type < 0 || type > ushort.MaxValue)
            throw new InvalidOperationException($"{tile.FullName}: tile registration failed ({type})");
        tile.Type = (ushort)type;
        if (type >= tiles.Length)
        {
            Array.Resize(ref tiles, type + 1);
            Array.Resize(ref methods, type + 1);
        }

        tiles[type] = tile;
        if (!subscribed)
        {
            Host.Native()->OnTilesInstalled(&Installed);
            subscribed = true;
        }

        uint flags = Flags(tile) | (1u << 24);
        methods[type] = flags;
        if (Host.Native()->TileEvents(type, flags, &Event) == 0)
            throw new InvalidOperationException($"{tile.FullName}: native tile hooks could not be installed");
        TileHooks.Install(flags, true);
    }

    internal static void Register(GlobalTile tile)
    {
        globals.Add(tile);
        uint flags = Flags(tile);
        if (Host.Native()->TileEvents(-1, flags, &Event) == 0)
            throw new InvalidOperationException($"{tile.FullName}: native global tile hooks could not be installed");
        TileHooks.Install(flags, false);
    }

    static uint Flags(ModType tile)
    {
        uint flags = 0;
        for (int i = 0; i < Events.Length; i++)
            if (Events[i].Any(name => Overrides(tile, name)))
                flags |= 1u << i;
        return flags;
    }

    [UnmanagedCallersOnly]
    static void Installed(int first, int last)
    {
        for (int i = first; i <= last; i++)
        {
            if (GetTile(i) is not ModTile tile)
                continue;
            try
            {
                tile.SetupContent();
                if (Main.tileLavaDeath[i])
                    Main.tileObsidianKill[i] = true;
                if (Main.tileSolid[i])
                    Main.tileNoSunLight[i] = true;
                Host.Native()->TileAnimation(i, tile.AnimationFrameHeight);
                if (Overrides(tile, nameof(ModTile.AnimateTile)))
                    animated.Add(tile);
                if (TileID.Sets.HasOutlines[i])
                {
                    string? path = tile.Mod.ResolveFile(tile.HighlightTexture.StartsWith(tile.Mod.Name + "/", StringComparison.Ordinal) ? tile.HighlightTexture[(tile.Mod.Name.Length + 1)..] + ".png" : tile.HighlightTexture + ".png");
                    if (path != null && System.IO.File.Exists(path))
                        TextureAssets.HighlightMask[i] = new GameObject(ModContent.Request<Texture2D>(tile.HighlightTexture).GameAsset);
                    else
                        TileID.Sets.HasOutlines[i] = false;
                }

                RoomNeed(TileID.Sets.RoomNeeds.CountsAsChair, ref TileID.Sets.RoomNeeds.CountsAsChairTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsTable, ref TileID.Sets.RoomNeeds.CountsAsTableTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsTorch, ref TileID.Sets.RoomNeeds.CountsAsTorchTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsDoor, ref TileID.Sets.RoomNeeds.CountsAsDoorTypes, i);
            }
            catch (Exception e)
            {
                ItemHooks.Report(tile.FullName + ".SetStaticDefaults", e);
            }
        }
    }

    static void RoomNeed(GameArray<bool> flags, ref GameArray<int> list, int type)
    {
        if (!flags[type] || list.AsSpan().Contains(type))
            return;
        int length = list.Length;
        list = list.Resized(length + 1);
        list[length] = type;
    }

    internal static void ContentReady()
    {
        if (ready || (!subscribed && globals.Count == 0))
            return;
        ready = true;
        CacheGlobalHooks();
        bool locked = TileObjectData.readOnlyData;
        TileObjectData.readOnlyData = false;
        try
        {
            foreach (GlobalTile global in globals)
                global.SetupContent();
        }
        finally
        {
            TileObjectData.readOnlyData = locked;
        }

        TileHooks.SetupAdjacency();
        foreach (Mod mod in Host.Mods)
            foreach (ModItem item in mod.GetContent<ModItem>())
            {
                Item template = new(item.Type);
                if (GetTile(template.createTile) == null)
                    continue;
                drops.TryAdd((template.createTile, template.placeStyle), item.Type);
                drops.TryAdd((template.createTile, -1), item.Type);
                TileID.Sets.CraftingStationItemId[template.createTile] = item.Type;
            }
    }

    internal static void AddMapEntry(ModTile tile, Color color, LocalizedText name, Func<string, int, int, string>? nameFunc)
    {
        if (nameFunc != null)
            TileHooks.AddMapName(tile.Type, name, nameFunc);
        byte* key = Host.Utf8(name.Key ?? tile.FullName);
        try
        {
            if (Host.Native()->TileMapEntry(tile.Type, *(uint*)&color, key) < 0)
                throw new InvalidOperationException($"{tile.FullName}: map entry could not be registered");
        }
        finally
        {
            Host.FreeUtf8(key);
        }
    }

    internal static void RegisterItemDrop(int type, int item, int[] styles)
    {
        if (styles.Length == 0)
            drops[(type, -1)] = item;
        else
            foreach (int style in styles)
                drops[(type, style)] = item;
    }

    internal static int DropType(int type, int style)
    {
        return drops.GetValueOrDefault((type, style), drops.GetValueOrDefault((type, -1)));
    }

    internal static IEnumerable<Item> DefaultDrops(ModTile tile, int i, int j)
    {
        int item = DropType(tile.Type, StyleAt(i, j));
        if (item > 0)
            yield return new Item(item);
    }

    internal static int StyleAt(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        var data = TileObjectData.GetTileData(tile.type, 0, 0);
        if (data.IsNull)
            return 0;
        int x = tile.frameX / Math.Max(1, data.CoordinateFullWidth), y = tile.frameY / Math.Max(1, data.CoordinateFullHeight);
        return (data.StyleHorizontal ? y / Math.Max(1, data.StyleLineSkip) * Math.Max(1, data.StyleWrapLimit) + x : x / Math.Max(1, data.StyleLineSkip) * Math.Max(1, data.StyleWrapLimit) + y) / Math.Max(1, data.StyleMultiplier);
    }

    internal static bool Multi(int type)
    {
        var d = TileObjectData.GetTileData(type, 0, 0);
        return !d.IsNull && (d.Width > 1 || d.Height > 1);
    }

    internal static void Animate()
    {
        foreach (ModTile tile in animated)
            tile.AnimateTile(ref Main.tileFrame[tile.Type], ref Main.tileFrameCounter[tile.Type]);
        foreach (GlobalTile global in Hooks.AnimateTile)
            global.AnimateTile();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawData
    {
        internal int FrameX, FrameY, Width, Height, Top, AddX, AddY;

        internal SpriteEffects Effects;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HighlightArgs
    {
        internal IntPtr Player;

        internal int Result;
    }

    [UnmanagedCallersOnly]
    static int Event(int kind, int i, int j, int type, void* args)
    {
        try
        {
            ModTile? tile = GetTile(type);
            if (kind == 19 || kind == 20)
            {
                ref HighlightArgs h = ref *(HighlightArgs*)args;
                bool? result = kind == 19 ? tile?.IsTileDangerous(i, j, new Player(h.Player)) == true ? true : null : tile?.IsTileSpelunkable(i, j) == true ? true : null;
                foreach (GlobalTile g in (kind == 19 ? Hooks.IsTileDangerous : Hooks.IsTileSpelunkable))
                {
                    bool? own = kind == 19 ? g.IsTileDangerous(i, j, type, new Player(h.Player)) : g.IsTileSpelunkable(i, j, type);
                    if (own == false)
                    {
                        h.Result = 0;
                        return 1;
                    }

                    if (own == true)
                        result = true;
                }

                if (result.HasValue)
                    h.Result = result.Value ? 1 : 0;
            }

            if (kind == 24)
            {
                return TileHooks.CheckObject(i, j, type) ? 1 : 0;
            }

            if (kind == 8)
            {
                bool underground = *(int*)args != 0;
                tile?.RandomUpdate(i, j, underground);
                foreach (GlobalTile g in Hooks.RandomUpdate)
                    g.RandomUpdate(i, j, type, underground);
            }

            if (kind == 17)
            {
                int* a = (int*)args;
                bool reset = a[0] != 0, keep = a[1] != 0;
                bool result = tile?.TileFrame(i, j, ref reset, ref keep) ?? true;
                foreach (GlobalTile g in Hooks.TileFrame)
                    result &= g.TileFrame(i, j, type, ref reset, ref keep);
                a[0] = reset ? 1 : 0;
                a[1] = keep ? 1 : 0;
                return result ? 1 : 0;
            }

            if (kind == 12)
            {
                ref DrawData d = ref *(DrawData*)args;
                short x = (short)d.FrameX, y = (short)d.FrameY;
                tile?.SetDrawPositions(i, j, ref d.Width, ref d.Top, ref d.Height, ref x, ref y);
                d.FrameX = x;
                d.FrameY = y;
                tile?.AnimateIndividualTile(type, i, j, ref d.AddX, ref d.AddY);
                tile?.SetSpriteEffects(i, j, ref d.Effects);
                foreach (GlobalTile g in Hooks.SetSpriteEffects)
                    g.SetSpriteEffects(i, j, type, ref d.Effects);
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report("TileLoader event " + kind, e);
        }

        return 1;
    }
}
