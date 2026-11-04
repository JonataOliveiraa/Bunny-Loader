using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class PlayerCrowdControlHooks
{
    sealed class State { internal bool Controlled; }
    static readonly EntityInstances<State> States = new();

    internal static void Set(Player player)
    {
        ItemHooks.Once("PlayerCustomCrowdControl", () =>
        {
            On_Player.get_CCed += IsCrowdControlled;
            On_Player.ResetEffects += Reset;
        });
        State? state = States.Get(player.Pointer);
        if (state == null) States.Set(player.Pointer, state = new State());
        state.Controlled = true;
    }

    static bool IsCrowdControlled(On_Player.orig_get_CCed orig, Player player) =>
        States.Get(player.Pointer)?.Controlled == true || orig(player);

    static void Reset(On_Player.orig_ResetEffects orig, Player player)
    {
        if (States.Get(player.Pointer) is State state) state.Controlled = false;
        orig(player);
    }
}
