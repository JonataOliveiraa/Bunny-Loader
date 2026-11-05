using System;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class ItemLoader
{
    public static bool CanStack(Item destination, Item source)
    {
        if (destination.IsNull || source.IsNull || destination == source || destination.type != source.type || destination.prefix != source.prefix) return false;
        foreach (GlobalItem? global in ItemHooks.GlobalsOf(destination))
        {
            if (global == null) continue;
            try { if (!global.CanStack(destination, source)) return false; }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.CanStack", e); return false; }
        }
        try { return ItemHooks.ModItemOf(destination)?.CanStack(source) ?? true; }
        catch (Exception e) { ItemHooks.Report("ModItem.CanStack", e); return false; }
    }

    public static bool CanStackInWorld(WorldItem destination, WorldItem source)
    {
        foreach (GlobalItem? global in ItemHooks.GlobalsOf(destination.inner))
        {
            if (global == null) continue;
            try { if (!global.CanStackInWorld(destination, source)) return false; }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.CanStackInWorld", e); return false; }
        }
        try { return ItemHooks.ModItemOf(destination.inner)?.CanStackInWorld(destination, source) ?? true; }
        catch (Exception e) { ItemHooks.Report("ModItem.CanStackInWorld", e); return false; }
    }

    public static void OnStack(Item destination, Item source, int numToTransfer) => Notify(destination, source, numToTransfer, false);

    static void Notify(Item destination, Item source, int count, bool split)
    {
        foreach (GlobalItem? global in ItemHooks.GlobalsOf(destination))
        {
            if (global == null) continue;
            try { if (split) global.SplitStack(destination, source, count); else global.OnStack(destination, source, count); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.Stack", e); }
        }
        try
        {
            ModItem? mod = ItemHooks.ModItemOf(destination);
            if (split) mod?.SplitStack(source, count); else mod?.OnStack(source, count);
        }
        catch (Exception e) { ItemHooks.Report("ModItem.Stack", e); }
    }

    internal static void NotifySplit(Item destination, Item source, int count) => Notify(destination, source, count, true);

    public static bool TryStackItems(Item destination, Item source, out int numTransferred, bool infiniteSource = false)
    {
        numTransferred = 0;
        if (!CanStack(destination, source)) return false;
        StackItems(destination, source, out numTransferred, infiniteSource);
        return true;
    }

    public static void StackItems(Item destination, Item source, out int numTransferred, bool infiniteSource = false, int numToTransfer = -1)
    {
        numTransferred = Math.Min(Math.Max(0, destination.maxStack - destination.stack), source.stack);
        if (numToTransfer >= 0) numTransferred = Math.Min(numTransferred, numToTransfer);
        if (numTransferred <= 0) return;
        OnStack(destination, source, numTransferred);
        destination.stack += numTransferred;
        if (!infiniteSource) source.stack -= numTransferred;
    }

    public static void SplitStack(Item destination, Item source, int numToTransfer)
    {
        if (numToTransfer <= 0 || numToTransfer >= source.stack) throw new ArgumentOutOfRangeException(nameof(numToTransfer));
        destination.stack = 0;
        destination.favorited = false;
        NotifySplit(destination, source, numToTransfer);
        destination.stack += numToTransfer;
        source.stack -= numToTransfer;
    }

    public static Item TransferWithLimit(Item source, int limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        Item destination = source.Clone();
        if (source.stack <= limit) source.TurnToAir();
        else SplitStack(destination, source, limit);
        return destination;
    }
}
