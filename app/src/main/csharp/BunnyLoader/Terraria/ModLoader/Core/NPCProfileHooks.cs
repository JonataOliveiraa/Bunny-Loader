using System;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCProfileHooks
{
    const int Roll = 0, Name = 1, Texture = 2, Head = 3;

    [ThreadStatic] static NPC rolling;

    internal static void ContentReady()
    {
        bool globals = NPCLoader.AnyGlobal(NPCHook.ModifyTownNPCProfile);
        bool own = false;
        for (int type = NPCLoader.VanillaCount; type < NPCLoader.NPCCount; type++)
            if ((NPCLoader.GetNPC(type)?.Extra & NPCHook.TownNPCProfile) != 0) own = true;
        if (!globals && !own) return;
        NativeApi* api = Host.Native();
        if (api->InstallTownProfiles(&Profile) == 0)
        {
            Log.Error("town NPC profiles: the game's profiles could not be hooked; TownNPCProfile is ignored");
            return;
        }
        On_NPC.GiveTownUniqueDataToNPCsThatNeedIt += GiveTownUniqueData;
        On_NPC.RerollVariation += RerollVariation;
        if (globals)
            for (int type = 1; type < NPCLoader.VanillaCount; type++) api->SetTownProfileCarrier(type, 1);
        for (int type = NPCLoader.VanillaCount; type < NPCLoader.NPCCount; type++)
        {
            ModNPC? npc = NPCLoader.GetNPC(type);
            if (npc == null) continue;
            if ((npc.Extra & NPCHook.TownNPCProfile) != 0 || globals && IsTownSample(type))
                if (api->SetTownProfileCarrier(type, 0) == 0) Log.Error($"town NPC profiles: {npc.FullName} could not get a profile");
        }
    }

    static bool IsTownSample(int type) =>
        new GameDictionary(ContentSamples.NpcsByNetId).TryGetValue(type, out IntPtr sample) && new NPC(sample).townNPC;

    static void GiveTownUniqueData(On_NPC.orig_GiveTownUniqueDataToNPCsThatNeedIt orig, int type, int nextNPC)
    {
        NPC previous = rolling;
        rolling = (uint)nextNPC < 200u ? Main.npc[nextNPC] : default;
        try { orig(type, nextNPC); }
        finally { rolling = previous; }
    }

    static bool RerollVariation(On_NPC.orig_RerollVariation orig, NPC self)
    {
        NPC previous = rolling;
        rolling = self;
        try { return orig(self); }
        finally { rolling = previous; }
    }

    static ITownNPCProfile? ProfileOf(int type, NPC npc)
    {
        if (!npc.IsNull) return NPCLoader.TownNPCProfile(npc);
        ModNPC? template = NPCLoader.GetNPC(type);
        return template != null && (template.Extra & NPCHook.TownNPCProfile) != 0 ? template.TownNPCProfile() : null;
    }

    [UnmanagedCallersOnly]
    static int Profile(int kind, int type, IntPtr npcPointer, nint* result)
    {
        try
        {
            NPC npc = npcPointer != IntPtr.Zero ? new NPC(npcPointer) : rolling;
            ITownNPCProfile? profile = ProfileOf(type, npc);
            if (profile == null) return 0;
            switch (kind)
            {
                case Roll:
                    *result = profile.RollVariation();
                    return 1;
                case Name:
                    string? name = profile.GetNameForVariant(npc);
                    if (name == null) return 0;
                    *result = GameString.New(name);
                    return 1;
                case Texture:
                    Asset<Texture2D>? texture = profile.GetTextureNPCShouldUse(npc);
                    if (texture == null || texture.IsDirect) return 0;
                    texture.Wait();
                    *result = texture.GameAsset;
                    return texture.GameAsset != IntPtr.Zero ? 1 : 0;
                case Head:
                    *result = profile.GetHeadTextureIndex(npc);
                    return 1;
            }
        }
        catch (Exception e) { ItemHooks.Report("TownNPCProfile", e); }
        return 0;
    }
}
