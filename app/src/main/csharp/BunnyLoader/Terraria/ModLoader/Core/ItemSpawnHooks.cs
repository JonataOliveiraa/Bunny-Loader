using System;
using System.Runtime.InteropServices;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemSpawnHooks
{
    static MethodRef method;
    static readonly int[] Layout = { -1, 8, -1, -1, -1, -1, 12, -1, -1, -1 };

    internal static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModItem.OnSpawn))) return;
        ItemHooks.Once("ItemOnSpawn", () => On_Item.NewItem_IEntitySource_Vector2_int_int_int_NewItemOwnership_Nullable_NewItemModifier_bool += NewItem);
    }

    static int NewItem(On_Item.orig_NewItem_IEntitySource_Vector2_int_int_int_NewItemOwnership_Nullable_NewItemModifier_bool orig,
        IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, GameNullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)
    {
        Action<WorldItem> handler = world =>
        {
            if (!modifier.IsNull) modifier.Invoke(world);
            Item item = world.inner;
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.OnSpawn(world, source); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.OnSpawn", e); }
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { g.OnSpawn(world, source); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnSpawn", e); }
            }
        };
        ref readonly MethodRef native = ref GameInterop.Method(ref method, Item.Class,
            "int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)", Layout);
        IntPtr klass = GameDelegates.ParamClass(native.Info, 7);
        var wrapped = new Item.NewItemModifier(GameDelegates.Create(handler, klass, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&Invoke));
        using var pin = new GameHandle(wrapped.Pointer);
        return orig(source, center, type, stack, prefix, ownership, velocity, wrapped, noBroadcast);
    }

    [UnmanagedCallersOnly]
    static void Invoke(IntPtr target, IntPtr item, IntPtr methodInfo)
    {
        try { GameDelegates.Get<Action<WorldItem>>(target)?.Invoke(new WorldItem(item)); }
        catch (Exception e) { GameDelegates.Report("Item.OnSpawn", e); }
    }
}
