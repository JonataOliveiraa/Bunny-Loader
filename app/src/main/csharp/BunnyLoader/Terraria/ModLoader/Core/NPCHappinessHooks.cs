using System;
using BunnyLoader.Interop;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class NPCHappinessHooks
{
    internal static void Want(Type type, Type baseType)
    {
        if (baseType == typeof(ModNPC))
            ItemHooks.Once("TownNPCMood", () => On_ShopHelper.AddHappinessReportText += AddHappinessReportText);
        if (ItemHooks.Overrides(type, baseType, nameof(ModNPC.ModifyNPCHappiness)))
            ItemHooks.Once("NPCHappiness", () => On_AllPersonalitiesModifier.ModifyShopPrice += ModifyShopPrice);
    }

    static void AddHappinessReportText(On_ShopHelper.orig_AddHappinessReportText orig, ShopHelper self, string? textKeyInCategory, GameObject substitutes)
    {
        NPC npc = self._currentNPCBeingTalkedTo;
        if (npc.IsNull || NPCLoader.GetNPC(npc.type) is not ModNPC modNPC)
        {
            orig(self, textKeyInCategory, substitutes);
            return;
        }
        string text = Language.GetTextValueWith(modNPC.GetLocalizationKey("TownNPCMood") + "." + textKeyInCategory, substitutes) ?? "";
        self._currentHappiness = self._currentHappiness + text + " ";
    }

    static void ModifyShopPrice(On_AllPersonalitiesModifier.orig_ModifyShopPrice orig, AllPersonalitiesModifier self, HelperInfo info, ShopHelper shopHelperInstance)
    {
        orig(self, info, shopHelperInstance);
        NPC npc = info.npc;
        if (npc.IsNull)
            return;
        var nearby = new bool[info.nearbyNPCsByType.Length];
        for (int k = 0; k < nearby.Length; k++)
            nearby[k] = info.nearbyNPCsByType[k];
        int biome = 0;
        if (NPCLoader.GetNPC(npc.type) is ModNPC template && NPCLoader.InstanceOf(npc) is ModNPC instance)
        {
            try { instance.ModifyNPCHappiness(biome, shopHelperInstance, nearby); }
            catch (Exception e) { ItemHooks.Report($"{template.FullName}.ModifyNPCHappiness", e); }
        }
        foreach (GlobalNPC? global in NPCLoader.Slots(npc))
        {
            if (global == null)
                continue;
            try { global.ModifyNPCHappiness(npc, biome, shopHelperInstance, nearby); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyNPCHappiness", e); }
        }
    }
}
