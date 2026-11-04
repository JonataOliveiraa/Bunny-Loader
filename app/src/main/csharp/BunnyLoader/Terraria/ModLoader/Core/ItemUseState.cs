using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemUseState
{
    sealed class State { internal bool StopOnHurt; }
    static readonly EntityInstances<State> States = new();
    internal static bool StopOnHurt(Item item) => States.Get(item.Pointer)?.StopOnHurt == true;

    internal static void SetStopOnHurt(Item item, bool value)
    {
        States.Set(item.Pointer, new State { StopOnHurt = value });
        PlayerHurtHooks.Want();
        ItemHooks.Once("ItemUseStateClone", () => On_Item.Clone += Clone);
    }

    static Item Clone(On_Item.orig_Clone orig, Item item)
    {
        Item clone = orig(item);
        if (States.Get(item.Pointer) is State state) States.Set(clone.Pointer, new State { StopOnHurt = state.StopOnHurt });
        return clone;
    }

    internal static void OnHurt(Player player)
    {
        if (player.itemAnimation <= 0 || !StopOnHurt(player.HeldItem)) return;
        player.channel = false;
        player.itemAnimation = 0;
        player.itemAnimationMax = 0;
    }
}
