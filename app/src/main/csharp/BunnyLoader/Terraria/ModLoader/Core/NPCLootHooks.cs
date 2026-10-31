using System;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCLootHooks
{
    static bool wanted;
    static IntPtr sampleMethod;

    internal static void Install(NPCMethod methods)
    {
        wanted |= (methods & (NPCMethod.ModifyNPCLoot | NPCMethod.ModifyGlobalLoot)) != 0;
        uint flags = 0;
        if ((methods & NPCMethod.PreKill) != 0) flags |= 1;
        if ((methods & NPCMethod.OnKill) != 0) flags |= 2;
        if ((methods & NPCMethod.BossLoot) != 0) flags |= 4;
        if (flags != 0 && Host.Native()->InstallNPCLootHooks(flags, &Event) == 0)
            throw new InvalidOperationException("Could not install the native NPC loot stages.");
    }

    internal static void ContentReady()
    {
        if (!wanted || Main.ItemDropsDB.IsNull) return;
        for (int type = -65; type < NPCLoader.NPCCount; type++)
        {
            if (!TryGetSample(type, out NPC npc)) continue;
            GameHandle? pin = null;
            try
            {
                if (NeedsInstance(npc))
                {
                    npc = NPC.New();
                    pin = new GameHandle(npc.Pointer);
                    npc.SetDefaults(type, default);
                }
                NPCLoader.ModifyNPCLoot(npc, new NPCLoot(type, Main.ItemDropsDB));
            }
            finally { pin?.Dispose(); }
        }
        NPCLoader.ModifyGlobalLoot(new GlobalLoot(Main.ItemDropsDB));
    }

    static bool NeedsInstance(NPC npc)
    {
        if (NPCLoader.Globals.Slots(npc) != null) return false;
        foreach (GlobalNPC global in NPCLoader.Globals.Templates)
        {
            if (!global.InstancePerEntity || (global.Hooks & NPCMethod.ModifyNPCLoot) == 0) continue;
            try { if (global.AppliesToEntity(npc, false)) return true; }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.AppliesToEntity", e); }
        }
        return false;
    }

    static bool TryGetSample(int type, out NPC npc)
    {
        GameObject samples = ContentSamples.NpcsByNetId;
        if (samples.IsNull) { npc = default; return false; }
        if (sampleMethod == IntPtr.Zero)
        {
            byte* name = Host.Utf8("TryGetValue");
            try { sampleMethod = Host.Native()->FindMethod(*(IntPtr*)samples.Pointer, name, 2); }
            finally { Host.FreeUtf8(name); }
            if (sampleMethod == IntPtr.Zero) throw new MissingMethodException("NPC samples have no TryGetValue method.");
        }
        IntPtr pointer = IntPtr.Zero;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(samples.Pointer);
        call.Arg(type);
        call.Arg((IntPtr)(&pointer));
        call.Arg(sampleMethod);
        call.Invoke(Host.Native()->MethodPointer(sampleMethod));
        npc = new(pointer);
        return !npc.IsNull;
    }

    [UnmanagedCallersOnly]
    static int Event(int kind, IntPtr pointer, int* args)
    {
        NPC npc = new(pointer);
        try
        {
            if (kind == 0) return NPCLoader.PreKill(npc) ? 1 : 0;
            if (kind == 1) NPCLoader.OnKill(npc);
            if (kind == 2)
            {
                if (!NPCLoader.HasHooks(npc, NPCMethod.BossLoot)) return 0;
                args[2] = Main.rand.Next(5) + 5;
                NPCLoader.BossLoot(npc, ref args[0], ref args[1], ref args[2]);
                args[2] = Math.Max(0, args[2]);
                return 1;
            }
        }
        catch (Exception e) { ItemHooks.Report("NPC loot", e); }
        return kind == 0 ? 1 : 0;
    }
}
