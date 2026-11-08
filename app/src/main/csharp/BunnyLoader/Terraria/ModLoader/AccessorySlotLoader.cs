using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.Default;

namespace Terraria.ModLoader;

public class AccessorySlotLoader
{
    internal static readonly AccessorySlotLoader Instance = new();

    internal readonly List<ModAccessorySlot> list = new();

    public const int MaxVanillaSlotCount = 2 + 5;

    static Player Player => Main.LocalPlayer;

    public int TotalCount => list.Count;

    internal static ModAccessorySlotPlayer ModSlotPlayer(Player player) => AccessorySlotHooks.Of(player);

    internal int Register(ModAccessorySlot slot)
    {
        ModTypeLookup<ModAccessorySlot>.Register(slot);
        lock (list)
        {
            list.Add(slot);
            AccessorySlotHooks.Install();
            return list.Count - 1;
        }
    }

    ModAccessorySlot GetIdCorrected(int id) => id >= list.Count ? new UnloadedAccessorySlot(id, "TEMP'd") : list[id];

    public ModAccessorySlot Get(int id, Player player) => GetIdCorrected(id % ModSlotPlayer(player).SlotCount);

    public ModAccessorySlot Get(int id) => Get(id, Player);

    public AccessorySlotType ContextToEnum(int context) => (AccessorySlotType)Math.Abs(context);

    public bool ModdedIsItemSlotUnlockedAndUsable(int index, Player player) => Get(index, player).IsEnabled();

    public bool ModdedIsSpecificItemSlotUnlockedAndUsable(int index, Player player, bool vanity)
    {
        ModAccessorySlot slot = Get(index, player);
        return !ModSlotPlayer(player).SharedSlotHasLoadoutConflict(index, vanity) && AccessorySlotHooks.WithPlayer(player, slot.IsEnabled);
    }

    public void CustomUpdateEquips(int index, Player player)
    {
        ModAccessorySlot slot = Get(index, player);
        AccessorySlotHooks.WithPlayer(player, slot.ApplyEquipEffects);
    }

    public bool ModdedCanSlotBeShown(int index) => Get(index).IsVisibleWhenNotEnabled();

    public bool IsHidden(int index) => Get(index).IsHidden();

    public bool CanAcceptItem(int index, Item checkItem, int context) => Get(index).CanAcceptItem(checkItem, ContextToEnum(context));

    public bool CanPlayerAcceptItem(Player player, int index, Item checkItem, int context)
    {
        ModAccessorySlot slot = Get(index, player);
        return AccessorySlotHooks.WithPlayer(player, () => slot.CanAcceptItem(checkItem, ContextToEnum(context)));
    }

    public void OnHover(int index, int context) => Get(index).OnMouseHover(ContextToEnum(context));

    public bool ModSlotCheck(Item checkItem, int slot, int context) =>
        CanAcceptItem(slot, checkItem, context) && !AccessorySlotHooks.HasConflict(Player, checkItem, slot, context);

    public bool IsAccessoryInConflict(Player player, Item checkItem, int slot, int context)
    {
        if (checkItem.IsAir)
            return false;
        if (!CanPlayerAcceptItem(player, slot, checkItem, context))
            return true;
        return AccessorySlotHooks.HasConflict(player, checkItem, slot, context);
    }

    public void ModifyDefaultSwapSlot(Item item, ref int accSlotToSwapTo)
    {
        for (int num = ModSlotPlayer(Player).SlotCount - 1; num >= 0; num--)
        {
            if (ModdedIsSpecificItemSlotUnlockedAndUsable(num, Player, vanity: false) && Get(num).ModifyDefaultSwapSlot(item, accSlotToSwapTo))
                accSlotToSwapTo = num + 20;
        }
    }

    public bool PreferredGolfBall(ref int projType)
    {
        ModAccessorySlotPlayer slots = ModSlotPlayer(Player);
        for (int num = slots.SlotCount * 2 - 1; num >= 0; num--)
        {
            if (!ModdedIsSpecificItemSlotUnlockedAndUsable(num % slots.SlotCount, Player, vanity: num >= slots.SlotCount))
                continue;
            Item item = slots.exAccessorySlot[num];
            if (!item.IsAir && item.shoot > 0 && ProjectileID.Sets.IsAGolfBall[item.shoot])
            {
                projType = item.shoot;
                return true;
            }
        }
        return false;
    }
}

public enum AccessorySlotType
{
    FunctionalSlot = 10,
    VanitySlot = 11,
    DyeSlot = 12
}

public static class LoaderManager
{
    public static T Get<T>() where T : class
    {
        if (typeof(T) == typeof(AccessorySlotLoader))
            return (T)(object)AccessorySlotLoader.Instance;
        throw new NotSupportedException($"LoaderManager.Get<{typeof(T).Name}>() is not available in Bunny Loader; use the static loader class instead");
    }
}
