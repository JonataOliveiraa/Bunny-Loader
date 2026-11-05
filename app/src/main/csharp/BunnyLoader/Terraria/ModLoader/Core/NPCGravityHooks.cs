using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class NPCGravityHooks
{
    internal sealed class State
    {
        internal MultipliableFloat Multiplier = MultipliableFloat.One;
        internal MultipliableFloat MaxFallSpeed = MultipliableFloat.One;
    }
    static readonly EntityInstances<State> States = new();

    internal static State Of(NPC npc)
    {
        ItemHooks.Once("NPCGravityMultiplier", () =>
        {
            On_NPC.UpdateNPC_BuffSetFlags += Reset;
            On_NPC.UpdateNPC_UpdateGravity += UpdateGravity;
        });
        State? value = States.Get(npc.Pointer);
        if (value == null) States.Set(npc.Pointer, value = new State());
        return value;
    }

    static void Reset(On_NPC.orig_UpdateNPC_BuffSetFlags orig, NPC npc, bool lowerBuffTime)
    {
        if (lowerBuffTime && States.Get(npc.Pointer) is State state)
        {
            state.Multiplier = MultipliableFloat.One;
            state.MaxFallSpeed = MultipliableFloat.One;
        }
        orig(npc, lowerBuffTime);
    }

    static void UpdateGravity(On_NPC.orig_UpdateNPC_UpdateGravity orig, NPC npc, out float maxFallSpeed)
    {
        orig(npc, out maxFallSpeed);
        if (States.Get(npc.Pointer) is State state)
        {
            NPC.gravity *= state.Multiplier.Value;
            maxFallSpeed *= state.MaxFallSpeed.Value;
        }
    }
}
