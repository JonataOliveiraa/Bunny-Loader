using System;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCGravityHooks
{
    internal sealed class State
    {
        internal MultipliableFloat Multiplier = MultipliableFloat.One;
        internal MultipliableFloat MaxFallSpeed = MultipliableFloat.One;
    }

    const int Slots = 200;
    static readonly EntityInstances<State> States = new();
    static byte* touched;
    static volatile bool outside;

    internal static State Of(NPC npc)
    {
        ItemHooks.Once("NPCGravityMultiplier", () =>
        {
            touched = NPCGates.Bitmap<On_NPC.hook_UpdateNPC_UpdateGravity>(0, "whoAmI", Slots);
            On_NPC.UpdateNPC_BuffSetFlags += Reset;
            On_NPC.UpdateNPC_UpdateGravity += UpdateGravity;
        });
        Touch(npc);
        State? value = States.Get(npc.Pointer);
        if (value == null) States.Set(npc.Pointer, value = new State());
        return value;
    }

    static int Slot(NPC npc)
    {
        int index = npc.whoAmI;
        return (uint)index < Slots && Main.npc[index] == npc ? index : -1;
    }

    static void Touch(NPC npc)
    {
        int slot = Slot(npc);
        if (slot >= 0)
        {
            NPCGates.Mark(touched, Slots, slot);
            return;
        }
        if (outside) return;
        outside = true;
        if (touched != null) new Span<byte>(touched, (Slots + 7) / 8).Fill(0xFF);
    }

    static bool Untouched(NPC npc, out int slot)
    {
        slot = outside || touched == null ? -1 : Slot(npc);
        return slot >= 0 && !NPCGates.Marked(touched, Slots, slot);
    }

    static void Reset(On_NPC.orig_UpdateNPC_BuffSetFlags orig, NPC npc, bool lowerBuffTime)
    {
        if (lowerBuffTime && !Untouched(npc, out int slot))
        {
            if (States.Get(npc.Pointer) is State state)
            {
                state.Multiplier = MultipliableFloat.One;
                state.MaxFallSpeed = MultipliableFloat.One;
            }
            if (slot >= 0 && !outside && touched != null) touched[slot >> 3] &= (byte)~(1 << (slot & 7));
        }
        orig(npc, lowerBuffTime);
    }

    static void UpdateGravity(On_NPC.orig_UpdateNPC_UpdateGravity orig, NPC npc, out float maxFallSpeed)
    {
        orig(npc, out maxFallSpeed);
        if (Untouched(npc, out _)) return;
        if (States.Get(npc.Pointer) is State state)
        {
            NPC.gravity *= state.Multiplier.Value;
            maxFallSpeed *= state.MaxFallSpeed.Value;
        }
    }
}
