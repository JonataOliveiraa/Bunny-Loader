using System;
using System.Collections.Generic;
using BunnyLoader;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCDefaultsHooks
{
    [ThreadStatic] static HashSet<(IntPtr, int)>? cloning;

    internal static void SetDefaults(On_NPC.orig_SetDefaults orig, NPC self, int type, NPCSpawnParams spawnparams)
    {
        ModNPC? template = NPCLoader.GetNPC(type);
        NPCSpawnParams reset = template == null ? spawnparams : new NPCSpawnParams
        {
            difficultyOverride = GameDifficultyLevel.Classic,
            sizeScaleOverride = 1f,
        };
        orig(self, type, reset);
        NPCLoader.SetDefaults(self, template);
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
