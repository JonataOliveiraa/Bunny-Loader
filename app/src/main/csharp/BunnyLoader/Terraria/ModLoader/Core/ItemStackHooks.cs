using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class ItemStackHooks
{
    sealed class Snapshot
    {
        internal Item Item;
        internal int Stack, Type, Remaining;
    }
    sealed class TransferScope
    {
        internal readonly Dictionary<IntPtr, Snapshot> Items = new();
        internal readonly List<(Item A, Item B)> Pairs = new();
        internal readonly List<(Item Source, Item Copy)> Clones = new();
        internal readonly List<Item> DeferredAir = new();
        internal readonly List<GameHandle> Pins = new();
        internal void Watch(Item item)
        {
            if (item.IsNull || Items.ContainsKey(item.Pointer)) return;
            Items[item.Pointer] = new() { Item = item, Stack = item.stack, Type = item.type, Remaining = item.stack };
            Pins.Add(new GameHandle(item.Pointer));
        }
    }
    [ThreadStatic] static TransferScope? current;

    internal static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModItem.CanStack)) && !has(nameof(ModItem.CanStackInWorld)) && !has(nameof(ModItem.OnStack)) && !has(nameof(ModItem.SplitStack))) return;
        ItemHooks.Once("ItemStacking", () =>
        {
            On_Item.CanStack_Item_Item += CanStack;
            On_WorldItem.TryCombiningIntoNearbyItems += World;
            On_ItemSlot.LeftClick_ItemArray_int_int += Left;
            On_ItemSlot.RightClick_ItemArray_int_int += Right;
            On_ItemSlot.PlaceItem += Place;
            On_Player.GetItem += Get;
            On_Item.Clone += Clone;
            On_Item.TurnToAir += TurnToAir;
        });
    }

    static bool CanStack(On_Item.orig_CanStack_Item_Item orig, Item a, Item b)
    {
        bool allowed = orig(a, b) && ItemLoader.CanStack(a, b);
        if (allowed && current is { } scope)
        {
            scope.Watch(a); scope.Watch(b);
            if (!scope.Pairs.Contains((a, b)) && !scope.Pairs.Contains((b, a))) scope.Pairs.Add((a, b));
        }
        return allowed;
    }

    static TransferScope? Begin()
    {
        if (current != null) return null;
        return current = new TransferScope();
    }

    static void End(TransferScope? scope)
    {
        if (scope == null) return;
        current = null;
        try
        {
            var deltas = new Dictionary<IntPtr, int>();
            foreach (var entry in scope.Items) deltas[entry.Key] = entry.Value.Item.stack - entry.Value.Stack;
            foreach (var pair in scope.Pairs)
            {
                int da = deltas[pair.A.Pointer], db = deltas[pair.B.Pointer];
                if (da == 0 || db == 0 || Math.Sign(da) == Math.Sign(db)) continue;
                Item destination = da > 0 ? pair.A : pair.B;
                Item source = da > 0 ? pair.B : pair.A;
                int count = Math.Min(Math.Abs(da), Math.Abs(db));
                Reconcile(scope, destination, source, count, false);
                deltas[destination.Pointer] -= count;
                deltas[source.Pointer] += count;
            }
            foreach (var clone in scope.Clones)
            {
                var before = scope.Items[clone.Source.Pointer];
                int count = clone.Copy.stack;
                if (clone.Copy.type != before.Type || count <= 0 || clone.Source.type != before.Type || clone.Source.stack <= 0 || clone.Source.stack >= before.Stack || count > before.Stack - clone.Source.stack) continue;
                Reconcile(scope, clone.Copy, clone.Source, count, true);
            }
        }
        catch (Exception e) { ItemHooks.Report("Item stack reconciliation", e); }
        finally
        {
            foreach (Item item in scope.DeferredAir) if (item.stack <= 0) item.TurnToAir();
            foreach (GameHandle pin in scope.Pins) pin.Dispose();
        }
    }

    static void Reconcile(TransferScope scope, Item destination, Item source, int count, bool split)
    {
        Snapshot from = scope.Items[source.Pointer];
        Snapshot to = scope.Items[destination.Pointer];

        if (source.type != from.Type || destination.type != from.Type) return;

        int sourceFinal = source.stack, destinationFinal = destination.stack;
        int sourceBefore = from.Remaining, destinationBefore = split ? 0 : to.Remaining;
        source.stack = sourceBefore;
        destination.stack = destinationBefore;
        
        if (split) { destination.favorited = false; ItemLoader.NotifySplit(destination, source, count); }
        else ItemLoader.OnStack(destination, source, count);
        
        int sourceAdjustment = source.stack - sourceBefore, destinationAdjustment = destination.stack - destinationBefore;
        source.stack = sourceFinal + sourceAdjustment;
        destination.stack = destinationFinal + destinationAdjustment;
        from.Remaining -= count;
        to.Remaining = destinationBefore + count;
    }

    static Item Clone(On_Item.orig_Clone orig, Item source)
    {
        current?.Watch(source);
        Item copy = orig(source);
        if (current is { } scope && !copy.IsNull && copy != source)
        {
            scope.Watch(copy);
            scope.Clones.Add((source, copy));
        }
        return copy;
    }

    static void TurnToAir(On_Item.orig_TurnToAir orig, Item item)
    {
        if (current is { } scope && item.stack <= 0 && scope.Items.ContainsKey(item.Pointer))
        {
            if (!scope.DeferredAir.Contains(item)) scope.DeferredAir.Add(item);
            return;
        }
        orig(item);
    }

    static void Left(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, GameArray<Item> inventory, int context, int slot)
    {
        TransferScope? scope = Begin();
        try { current?.Watch(Main.mouseItem); if ((uint)slot < (uint)inventory.Length) current?.Watch(inventory[slot]); orig(inventory, context, slot); }
        finally { End(scope); }
    }

    static bool Right(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, GameArray<Item> inventory, int context, int slot)
    {
        TransferScope? scope = Begin();
        try { current?.Watch(Main.mouseItem); if ((uint)slot < (uint)inventory.Length) current?.Watch(inventory[slot]); return orig(inventory, context, slot); }
        finally { End(scope); }
    }

    static void Place(On_ItemSlot.orig_PlaceItem orig, ref GameArray<Item> inventory, ref Item item, int context, int slot, int keepStack)
    {
        TransferScope? scope = Begin();
        try { current?.Watch(item); foreach (Item existing in inventory) current?.Watch(existing); orig(ref inventory, ref item, context, slot, keepStack); }
        finally { End(scope); }
    }

    static Item Get(On_Player.orig_GetItem orig, Player player, Item item, GetItemSettings settings, bool disableMerge)
    {
        TransferScope? scope = Begin();
        try { current?.Watch(item); foreach (Item existing in player.inventory) current?.Watch(existing); return orig(player, item, settings, disableMerge); }
        finally { End(scope); }
    }

    static void World(On_WorldItem.orig_TryCombiningIntoNearbyItems orig, WorldItem self)
    {
        if (self.playerIndexTheItemIsReservedFor != Main.myPlayer || self.shimmerTime > 0f || !self.inner.CanPassivelyStackInWorld() || self.stack >= self.maxStack) return;
        for (int i = self.whoAmI + 1; i < Math.Min(Main.maxItems, Main.item.Length); i++)
        {
            WorldItem other = Main.item[i];
            if (other.IsNull || other.IsAir || !Item.CanStack(self.inner, other.inner) || !ItemLoader.CanStackInWorld(self, other) || other.shimmered != self.shimmered || other.playerIndexTheItemIsReservedFor != self.playerIndexTheItemIsReservedFor || Math.Abs(self.position.X - other.position.X) + Math.Abs(self.position.Y - other.position.Y) > 30f) continue;
            ItemLoader.StackItems(self.inner, other.inner, out int count);
            if (count <= 0) continue;
            float amount = (float)count / Math.Max(1, self.stack);
            self.position = Vector2.Lerp(other.position, self.position, amount);
            self.velocity = Vector2.Lerp(other.velocity, self.velocity, amount);
            if (other.stack <= 0) other.TurnToAir();
            if (Main.netMode != 0) { self.SyncItem(); other.SyncItem(); }
        }
    }
}
