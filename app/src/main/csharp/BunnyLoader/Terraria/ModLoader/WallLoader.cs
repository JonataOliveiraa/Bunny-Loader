using System;
using System.Collections.Generic;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe partial class WallLoader
{
    static ModWall?[] walls = new ModWall?[WallID.Count];

    static readonly Dictionary<int, int> wallTypeToItemType = new();

    static Dictionary<int, int>? placedBy;

    static bool subscribed;

    internal static readonly List<ModWall> Animated = new();

    public static int WallCount => Math.Max((int)WallID.Count, walls.Length);

    public static ModWall? GetWall(int type) => (uint)type < (uint)walls.Length ? walls[type] : null;

    internal static void Register(ModWall wall)
    {
        byte* mod = Host.Utf8(wall.Mod.Uid), name = Host.Utf8(wall.Name), texture = Host.Utf8(ItemLoader.ResolveTexture(wall, wall.Texture));
        int type;
        try
        {
            type = Host.Native()->RegisterWall(mod, name, texture);
        }
        finally
        {
            Host.FreeUtf8(mod);
            Host.FreeUtf8(name);
            Host.FreeUtf8(texture);
        }

        if (type < 0 || type > ushort.MaxValue)
            throw new InvalidOperationException($"{wall.FullName}: wall registration failed ({type})");
        wall.Type = (ushort)type;
        if (type >= walls.Length)
            Array.Resize(ref walls, type + 1);
        walls[type] = wall;
        if (!subscribed)
        {
            Host.Native()->OnWallsInstalled(&Installed);
            subscribed = true;
        }

        WallHooks.Register(wall);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    static void Installed(int first, int last)
    {
        for (int i = first; i <= last; i++)
        {
            if (GetWall(i) is not ModWall wall)
                continue;
            try
            {
                wall.SetupContent();
            }
            catch (Exception e)
            {
                ItemHooks.Report(wall.FullName + ".SetStaticDefaults", e);
            }
        }
    }

    internal static void AddMapEntry(ModWall wall, Color color, LocalizedText name)
    {
        byte* key = Host.Utf8(name.Key ?? wall.FullName);
        try
        {
            if (Host.Native()->WallMapEntry(wall.Type, *(uint*)&color, key) < 0)
                throw new InvalidOperationException($"{wall.FullName}: map entry could not be registered");
        }
        finally
        {
            Host.FreeUtf8(key);
        }
    }

    internal static void RegisterItemDrop(int type, int item) => wallTypeToItemType[type] = item;

    internal static int ItemDrop(ModWall wall)
    {
        if (wallTypeToItemType.TryGetValue(wall.Type, out int item))
            return item;
        if (placedBy == null)
        {
            placedBy = new Dictionary<int, int>();
            foreach (Mod mod in Host.Mods)
                foreach (ModItem modItem in mod.GetContent<ModItem>())
                {
                    Item template = new(modItem.Type);
                    if (GetWall(template.createWall) != null)
                        placedBy.TryAdd(template.createWall, modItem.Type);
                }
        }

        return placedBy.GetValueOrDefault(wall.Type);
    }
}
