using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCDefaultsHooks
{
    [ThreadStatic] static HashSet<(IntPtr, int)>? cloning;
    [ThreadStatic] static DefaultsContext current;

    struct DefaultsContext
    {
        public NPC NPC;
        public ModNPC? Template;
        public bool Handled;
        public bool[]? Immunity;
        public int Type;
    }

    internal static void BeforeScaling(On_NPC.orig_ScaleStats orig, NPC npc, GameNullable<int> activePlayersCount, GameNullable<float> strengthOverride)
    {
        if (current.NPC == npc && current.Immunity is bool[] saved)
        {
            saved.AsSpan().CopyTo(npc.buffImmune.AsSpan());
            current.Immunity = null;
        }
        orig(npc, activePlayersCount, strengthOverride);
    }

    internal static void BeforeVanillaTail(NPC npc)
    {
        if (current.NPC != npc || current.Template != null || current.Handled || npc.type <= 0) return;
        current.Handled = true;
        NPCLoader.SetDefaults(npc, null);
        current.Immunity = npc.buffImmune.AsSpan().ToArray();
    }

    internal static void SetDefaults(On_NPC.orig_SetDefaults orig, NPC self, int type, NPCSpawnParams spawnparams)
    {
        ModNPC? template = NPCLoader.GetNPC(type);
        NPCSpawnParams reset = template == null ? spawnparams : new NPCSpawnParams
        {
            difficultyOverride = GameDifficultyLevel.Classic,
            sizeScaleOverride = 1f,
        };
        DefaultsContext outer = current;
        current = new DefaultsContext { NPC = self, Template = template, Type = type };
        try
        {
            orig(self, type, reset);
            if (!current.Handled)
            {
                current.Handled = true;
                NPCLoader.SetDefaults(self, template);
            }
            if (current.Immunity is bool[] saved) saved.AsSpan().CopyTo(self.buffImmune.AsSpan());
        }
        finally
        {
            bool handled = current.Handled;
            current = outer;
            if (handled && type >= 0 && current.NPC == self && current.Type < 0) current.Handled = true;
        }
        if (template == null) return;
        Host.Native()->FinishNPC(self.Pointer, type, &spawnparams);
        float scale = self.scale;
        if (spawnparams.sizeScaleOverride.HasValue)
        {
            self.position.X += (int)(self.width * scale) / 2;
            self.position.Y += (int)(self.height * scale);
            self.scale = scale = spawnparams.sizeScaleOverride.Value;
        }
        self.width = (int)(self.width * scale);
        self.height = (int)(self.height * scale);
        if (spawnparams.sizeScaleOverride.HasValue)
        {
            if (self.height == 16 || self.height == 32) self.height++;
            self.position.X -= self.width / 2;
            self.position.Y -= self.height;
        }
    }

    internal static void CloneDefaults(NPC npc, int type)
    {
        if (npc.IsNull) throw new ArgumentException("CloneDefaults requires an NPC", nameof(npc));
        if (type == npc.type || type >= NPCLoader.NPCCount)
            throw new ArgumentOutOfRangeException(nameof(type), type, "CloneDefaults requires a different registered NPC type");
        var key = (npc.Pointer, type);
        HashSet<(IntPtr, int)> active = cloning ??= new();
        if (!active.Add(key)) throw new InvalidOperationException($"NPC.CloneDefaults: circular defaults for type {type}");
        int originalType = npc.type, originalNetId = npc.netID;
        ModNPC? originalMod = npc.ModNPC;
        GlobalNPC?[]? originalGlobals = NPCLoader.Globals.Slots(npc);
        try
        {
            npc.SetDefaultsKeepPlayerInteraction(type, new NPCSpawnParams
            {
                difficultyOverride = GameDifficultyLevel.Classic,
                sizeScaleOverride = 1f,
            });
        }
        finally
        {
            npc.type = originalType;
            npc.netID = originalNetId;
            NPCLoader.Restore(npc, originalMod, originalGlobals);
            active.Remove(key);
        }
    }
}
