using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class PlayerPotionDelayHooks
{
    sealed class State { internal StatModifier Modifier = StatModifier.Default; }
    static readonly EntityInstances<State> States = new();

    internal static ref StatModifier Get(Player player)
    {
        ItemHooks.Once("PlayerPotionDelayModifier", () =>
        {
            ItemHealHooks.Install(static name => name == nameof(ModItem.ModifyPotionDelay));
            PlayerHooks.WantResetPotionDelay();
        });
        State? state = States.Get(player.Pointer);
        if (state == null) States.Set(player.Pointer, state = new State());
        return ref state.Modifier;
    }

    internal static int Apply(Player player, int delay)
    {
        State? state = States.Get(player.Pointer);
        if (state == null) return delay;
        float value = state.Modifier.ApplyTo(delay);
        return float.IsFinite(value) ? (int)System.Math.Clamp(value, 0f, int.MaxValue) : delay;
    }

    internal static void BeforeReset(Player player)
    {
        if (States.Get(player.Pointer) is State state) state.Modifier = StatModifier.Default;
    }
}
