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

    internal static IReadOnlyList<GlobalTile> Globals => globals;

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
            "SpecialDraw",
            "EmitParticles"
        },
        Array.Empty<string>(),
        new[]
        {
            "CanExplode"
        },
        new[]
        {
            "AdjTiles"
        },
        new[]
        {
            "HasSmartInteract",
            "RightClick"
        }
    };

    public static int TileCount => tiles.Length;

    public static bool CanGrowModCactus(int type) => PlantLoader.Exists(TileID.Cactus, type);

    public static Texture2D? GetCactusTexture(int type) => PlantLoader.Get<ModCactus>(TileID.Cactus, type)?.GetTexture().Value;

    public static ModTile? GetTile(int type) => (uint)type < (uint)tiles.Length ? tiles[type] : null;

    internal static ModTile? At(int i, int j) => GetTile(Main.tile[i, j].type);

    static bool[] emitters = Array.Empty<bool>();

    internal static bool EmitsParticles(int type) => (uint)type < (uint)emitters.Length && emitters[type];

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
        if (Overrides(tile, nameof(ModTile.EmitParticles)))
        {
            Array.Resize(ref emitters, Math.Max(emitters.Length, type + 1));
            emitters[type] = true;
        }

        TileHooks.InstallExtras(tile);
        TileHooks.InstallWorld(tile);
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
        if (tile is ModPylon pylon)
            PylonHooks.Add(pylon);
    }

    internal static void Mark(int type, uint flag)
    {
        if (Host.Native()->TileEvents(type, flag, &Event) == 0)
            throw new InvalidOperationException($"tile {type}: native tile hooks could not be installed");
    }

    internal static void Register(GlobalTile tile)
    {
        globals.Add(tile);
        TileHooks.InstallWorld(tile);
        TileHooks.WantReplace(tile);
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

    static void ResetTables(int type)
    {
        Main.tileFrameImportant[type] = false;
        Main.tileSolid[type] = false;
        Main.tileSolidTop[type] = false;
        Main.tileNoAttach[type] = false;
        Main.tileLavaDeath[type] = false;
        Main.tileWaterDeath[type] = false;
        Main.tileCut[type] = false;
        Main.tileTable[type] = false;
        Main.tileBlockLight[type] = false;
        Main.tileNoSunLight[type] = false;
        Main.tileLighted[type] = false;
        Main.tileNoFail[type] = false;
        Main.tileObsidianKill[type] = false;
        Main.tileMergeDirt[type] = false;
        Main.tileBrick[type] = false;
    }

    public static void ModifySmartInteractCoords(int type, ref int width, ref int height, ref int frameWidth, ref int frameHeight, ref int extraY)
    {
        if (GetTile(type) is not ModTile tile)
            return;
        TileObjectData data = TileObjectData.GetTileData(type, 0);
        if (data.IsNull)
            return;
        width = data.Width;
        height = data.Height;
        frameWidth = data.CoordinateWidth + data.CoordinatePadding;
        frameHeight = data.CoordinateHeights[0] + data.CoordinatePadding;
        extraY = data.CoordinateFullHeight % frameHeight;
        tile.ModifySmartInteractCoords(ref width, ref height, ref frameWidth, ref frameHeight, ref extraY);
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
                ResetTables(i);
                tile.SetupContent();
                if (Main.tileLavaDeath[i])
                    Main.tileObsidianKill[i] = true;
                if (Main.tileSolid[i])
                    Main.tileNoSunLight[i] = true;
                tile.PostSetDefaults();
                if (tile.IsDoor)
                    TileHooks.InstallDoors();
                if (TileID.Sets.Clock[i])
                {
                    Mark(i, 1u << 9);
                    TileHooks.Install(1u << 9, false);
                }
                Host.Native()->TileAnimation(i, tile.AnimationFrameHeight);
                if (Overrides(tile, nameof(ModTile.AnimateTile)))
                    animated.Add(tile);
                if (TileID.Sets.HasOutlines[i])
                {
                    string? path = tile.Mod.ResolveFile(tile.HighlightTexture.StartsWith(tile.Mod.Name + "/", StringComparison.Ordinal) ? tile.HighlightTexture[(tile.Mod.Name.Length + 1)..] + ".png" : tile.HighlightTexture + ".png");
                    if (path != null && System.IO.File.Exists(path))
                        TextureAssets.HighlightMask[i] = ModContent.Request<Texture2D>(tile.HighlightTexture);
                    else
                        TileID.Sets.HasOutlines[i] = false;
                }

                RoomNeed(TileID.Sets.RoomNeeds.CountsAsChair, ref TileID.Sets.RoomNeeds.CountsAsChairTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsTable, ref TileID.Sets.RoomNeeds.CountsAsTableTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsTorch, ref TileID.Sets.RoomNeeds.CountsAsTorchTypes, i);
                RoomNeed(TileID.Sets.RoomNeeds.CountsAsDoor, ref TileID.Sets.RoomNeeds.CountsAsDoorTypes, i);
                ChestHooks.Install(tile);
                if (Main.tileSign[i])
                    PageIconHooks.InstallSign();
            }
            catch (Exception e)
            {
                ItemHooks.Report(tile.FullName + ".SetStaticDefaults", e);
            }
        }
        FitPylonSceneMetrics();
    }

    static void FitPylonSceneMetrics()
    {
        TeleportPylonsSystem pylons = Main.PylonSystem;
        if (pylons.IsNull)
            return;
        SceneMetrics metrics = pylons._sceneMetrics;
        if (!metrics.IsNull && !metrics._tileCounts.IsNull && metrics._tileCounts.Length < TileCount)
            metrics._tileCounts = metrics._tileCounts.Resized(TileCount);
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
                if (ItemID.Sets.DisableAutomaticPlaceableDrop[item.Type] || GetTile(template.createTile) == null)
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

    public static string DefaultContainerName(int type, int frameX, int frameY) =>
        GetTile(type) is ModTile tile && tile.DefaultContainerName(frameX, frameY) is { IsNull: false } name ? name.Value ?? string.Empty : string.Empty;

    public static bool CanExplode(int i, int j)
    {
        int type = Main.tile[i, j].type;
        if (GetTile(type)?.CanExplode(i, j) == false)
            return false;
        foreach (GlobalTile g in Hooks.CanExplode)
            if (!g.CanExplode(i, j, type))
                return false;
        return true;
    }

    public static bool IsLockedChest(int i, int j, int type) => GetTile(type)?.IsLockedChest(i, j) ?? false;

    public static bool UnlockChest(int i, int j, int type, ref short frameXAdjustment, ref int dustType, ref bool manual) =>
        GetTile(type)?.UnlockChest(i, j, ref frameXAdjustment, ref dustType, ref manual) ?? false;

    public static bool LockChest(int i, int j, int type, ref short frameXAdjustment, ref bool manual) =>
        GetTile(type)?.LockChest(i, j, ref frameXAdjustment, ref manual) ?? false;

    internal static void RegisterItemDrop(int type, int item, int[] styles)
    {
        if (styles.Length == 0)
            drops[(type, -1)] = item;
        else
            foreach (int style in styles)
                drops[(type, style)] = item;
    }

    public static int GetItemDropFromTypeAndStyle(int type, int style = 0) => DropType(type, style);

    public static void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.type;
        GetTile(type)?.SetDrawPositions(i, j, ref width, ref offsetY, ref height, ref tileFrameX, ref tileFrameY);
    }

    public static void SetAnimationFrame(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        if (GetTile(type) is not ModTile tile)
            return;
        frameYOffset = tile.AnimationFrameHeight * Main.tileFrame[type];
        tile.AnimateIndividualTile(type, i, j, ref frameXOffset, ref frameYOffset);
    }

    public static int OpenDoorID(Tile tile)
    {
        if (GetTile(tile.type) is ModTile mod)
            return TileID.Sets.OpenDoorID[mod.Type];
        return tile.type == TileID.ClosedDoor && (tile.frameY < 594 || tile.frameY > 646 || tile.frameX >= 54) ? TileID.OpenDoor : -1;
    }

    public static int CloseDoorID(Tile tile)
    {
        if (GetTile(tile.type) is ModTile mod)
            return TileID.Sets.CloseDoorID[mod.Type];
        return tile.type == TileID.OpenDoor ? TileID.ClosedDoor : -1;
    }

    public static bool IsClosedDoor(Tile tile) => IsClosedDoor(tile.type);

    public static bool IsClosedDoor(int type) => GetTile(type) != null ? TileID.Sets.OpenDoorID[type] > -1 : type == TileID.ClosedDoor;

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

    internal static int StyleAt(int i, int j) => StyleOf(Main.tile[i, j]);

    internal static int StyleOf(Tile tile)
    {
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
