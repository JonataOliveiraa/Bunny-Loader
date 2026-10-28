using System.Runtime.InteropServices;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemDrawHooks
{
    [StructLayout(LayoutKind.Sequential)]
    struct WorldArgs
    {
        public Color Light;
        public Color Alpha;
        public float Rotation;
        public float Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InventoryArgs
    {
        public IntPtr Item;
        public IntPtr Batch;
        public Vector2 Position;
        public Rectangle Frame;
        public Color Light;
        public Color Environment;
        public Vector2 Origin;
        public float Scale;
    }

    const int World = 0, Inventory = 1;

    static Hook? drawItem, drawIcon, getAlpha;

    static bool globalWorld, globalInventory, globalAlpha;

    static byte* worldActive, inventoryActive;

    static IntPtr worldItem;
    static int worldIndex;
    static bool worldDrawn;
    static WorldArgs worldArgs;

    static bool inventoryDrawn;
    static InventoryArgs inventoryArgs;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.PreDrawInWorld)) || has(nameof(ModItem.PostDrawInWorld)))
        {
            if (global) globalWorld = true;
            ItemHooks.Once("DrawWorld", () =>
            {
                worldActive = (byte*)NativeMemory.AllocZeroed(1);
                if (Host.Native()->ItemDrawListen(World, &WorldPre, &WorldPost, worldActive) == 0)
                {
                    Log.Error("item hooks: the world item drawing stages are not hooked (PreDrawInWorld/PostDrawInWorld)");
                    return;
                }
                drawItem = ItemHooks.Raw("Terraria", "Main", "void DrawItem(WorldItem item, int whoami)",
                    (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int, IntPtr, void>)&DrawItem);
            });
        }
        if (has(nameof(ModItem.PreDrawInInventory)) || has(nameof(ModItem.PostDrawInInventory)))
        {
            if (global) globalInventory = true;
            ItemHooks.Once("DrawInventory", () =>
            {
                inventoryActive = (byte*)NativeMemory.AllocZeroed(1);
                if (Host.Native()->ItemDrawListen(Inventory, &InventoryPre, &InventoryPost, inventoryActive) == 0)
                {
                    Log.Error("item hooks: the inventory item drawing stages are not hooked (PreDrawInInventory/PostDrawInInventory)");
                    return;
                }
                drawIcon = ItemHooks.Raw("Terraria.UI", "ItemSlot",
                    "float DrawItemIcon(Item item, int context, SpriteBatch spriteBatch, Vector2 screenPositionForItemCenter, float scale, float sizeLimit, Color environmentColor, float itemFade, bool flip)",
                    (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, float, float, float, float, uint, float, byte, IntPtr, float>)&DrawItemIcon);
            });
        }
        if (has(nameof(ModItem.GetAlpha)))
        {
            if (global) globalAlpha = true;
            ItemHooks.Once("GetAlpha", () => getAlpha = ItemHooks.Raw("Terraria", "Item", "Color GetAlpha(Color newColor)",
                (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, uint>)&GetAlpha));
        }
    }

    static bool Wanted(bool global, Item item) => !item.IsNull && (global || ItemLoader.Get(item.type) != null);

    [UnmanagedCallersOnly]
    static void DrawItem(IntPtr self, IntPtr world, int whoami, IntPtr method)
    {
        Hook? hook = drawItem;
        if (hook == null) return;
        IntPtr outerItem = worldItem;
        int outerIndex = worldIndex;
        bool outerDrawn = worldDrawn;
        WorldArgs outerArgs = worldArgs;
        byte outerActive = *worldActive;
        bool wanted = false;
        try { wanted = world != IntPtr.Zero && Wanted(globalWorld, new WorldItem(world).inner); }
        catch (Exception e) { ItemHooks.Report("PreDrawInWorld", e); }
        if (wanted)
        {
            worldItem = world;
            worldIndex = whoami;
            worldDrawn = false;
            *worldActive = 1;
        }
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(self);
            call.Arg(world);
            call.Arg(whoami);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "Main.DrawItem");
        }
        finally
        {
            worldItem = outerItem;
            worldIndex = outerIndex;
            worldDrawn = outerDrawn;
            worldArgs = outerArgs;
            *worldActive = outerActive;
        }
    }

    [UnmanagedCallersOnly]
    static int WorldPre(void* raw)
    {
        if (worldItem == IntPtr.Zero) return 1;
        try
        {
            var args = (WorldArgs*)raw;
            Item item = new WorldItem(worldItem).inner;
            SpriteBatch batch = Main.spriteBatch;
            float rotation = args->Rotation, scale = args->Scale;
            bool go = true;
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { go &= m.PreDrawInWorld(batch, args->Light, args->Alpha, ref rotation, ref scale, worldIndex); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.PreDrawInWorld", e); }
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { go &= g.PreDrawInWorld(item, batch, args->Light, args->Alpha, ref rotation, ref scale, worldIndex); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.PreDrawInWorld", e); }
            }
            args->Rotation = rotation;
            args->Scale = scale;
            worldArgs = *args;
            worldDrawn = true;
            return go ? 1 : 0;
        }
        catch (Exception e)
        {
            ItemHooks.Report("PreDrawInWorld", e);
            return 1;
        }
    }

    [UnmanagedCallersOnly]
    static void WorldPost()
    {
        if (worldItem == IntPtr.Zero || !worldDrawn) return;
        try
        {
            Item item = new WorldItem(worldItem).inner;
            SpriteBatch batch = Main.spriteBatch;
            WorldArgs a = worldArgs;
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.PostDrawInWorld(batch, a.Light, a.Alpha, a.Rotation, a.Scale, worldIndex); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostDrawInWorld", e); }
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { g.PostDrawInWorld(item, batch, a.Light, a.Alpha, a.Rotation, a.Scale, worldIndex); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostDrawInWorld", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("PostDrawInWorld", e); }
    }

    [UnmanagedCallersOnly]
    static float DrawItemIcon(IntPtr item, int context, IntPtr batch, float x, float y, float scale, float sizeLimit,
                              uint environment, float fade, byte flip, IntPtr method)
    {
        Hook? hook = drawIcon;
        if (hook == null) return scale;
        bool outerDrawn = inventoryDrawn;
        InventoryArgs outerArgs = inventoryArgs;
        byte outerActive = *inventoryActive;
        bool wanted = false;
        try { wanted = Wanted(globalInventory, new Item(item)); }
        catch (Exception e) { ItemHooks.Report("PreDrawInInventory", e); }
        if (wanted)
        {
            inventoryDrawn = false;
            *inventoryActive = 1;
        }
        try
        {
            var call = GameCall.Returning(RetKind.F32);
            call.Arg(item);
            call.Arg(context);
            call.Arg(batch);
            call.Arg(x);
            call.Arg(y);
            call.Arg(scale);
            call.Arg(sizeLimit);
            call.Arg((long)environment);
            call.Arg(fade);
            call.Arg(flip);
            call.Arg(method);
            long result = ItemHooks.Original(ref call, hook, "ItemSlot.DrawItemIcon");
            return Abi.ToF32((ulong)result);
        }
        finally
        {
            inventoryDrawn = outerDrawn;
            inventoryArgs = outerArgs;
            *inventoryActive = outerActive;
        }
    }

    [UnmanagedCallersOnly]
    static int InventoryPre(void* raw)
    {
        try
        {
            var args = (InventoryArgs*)raw;
            var item = new Item(args->Item);
            if (item.IsNull) return 1;
            var batch = new SpriteBatch(args->Batch);
            Color drawColor = item.GetAlpha(args->Light), itemColor = item.GetColor(args->Environment);
            bool go = true;
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { go &= g.PreDrawInInventory(item, batch, args->Position, args->Frame, drawColor, itemColor, args->Origin, args->Scale); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.PreDrawInInventory", e); }
            }
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { go &= m.PreDrawInInventory(batch, args->Position, args->Frame, drawColor, itemColor, args->Origin, args->Scale); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.PreDrawInInventory", e); }
            }
            inventoryArgs = *args;
            inventoryDrawn = true;
            return go ? 1 : 0;
        }
        catch (Exception e)
        {
            ItemHooks.Report("PreDrawInInventory", e);
            return 1;
        }
    }

    [UnmanagedCallersOnly]
    static void InventoryPost()
    {
        if (!inventoryDrawn) return;
        try
        {
            InventoryArgs a = inventoryArgs;
            var item = new Item(a.Item);
            var batch = new SpriteBatch(a.Batch);
            Color drawColor = item.GetAlpha(a.Light), itemColor = item.GetColor(a.Environment);
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.PostDrawInInventory(batch, a.Position, a.Frame, drawColor, itemColor, a.Origin, a.Scale); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostDrawInInventory", e); }
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { g.PostDrawInInventory(item, batch, a.Position, a.Frame, drawColor, itemColor, a.Origin, a.Scale); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostDrawInInventory", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("PostDrawInInventory", e); }
    }

    [UnmanagedCallersOnly]
    static uint GetAlpha(IntPtr self, uint color, IntPtr method)
    {
        Hook? hook = getAlpha;
        if (hook == null) return color;
        try
        {
            var item = new Item(self);
            if (Wanted(globalAlpha, item) && Alpha(item, *(Color*)&color) is Color changed) return *(uint*)&changed;
        }
        catch (Exception e) { ItemHooks.Report("GetAlpha", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg((long)color);
        call.Arg(method);
        return (uint)ItemHooks.Original(ref call, hook, "Item.GetAlpha");
    }

    static Color? Alpha(Item item, Color light)
    {
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (g.GetAlpha(item, light) is Color c) return c;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.GetAlpha", e); }
        }
        if (ItemHooks.ModItemOf(item) is not ModItem m) return null;
        try { return m.GetAlpha(light); }
        catch (Exception e)
        {
            ItemHooks.Report($"{m.FullName}.GetAlpha", e);
            return null;
        }
    }
}
