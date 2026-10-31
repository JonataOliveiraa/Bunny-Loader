using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    public static void CanTownNPCSpawn(int numTownNPCs)
    {
        foreach (ModNPC? own in npcs)
        {
            if (own == null || !own.NPC.townNPC || !Has(own, NPCMethod.TownSpawn) || NPCHeadLoader.GetNPCHeadSlot(own.Type) < 0 || NPC.AnyNPCs(own.Type)) continue;
            try
            {
                if (!own.CanTownNPCSpawn(numTownNPCs)) continue;
                Main.townNPCCanSpawn[own.Type] = true;
                if (WorldGen.prioritizedTownNPCType == 0) WorldGen.prioritizedTownNPCType = own.Type;
            }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.CanTownNPCSpawn", e); }
        }
    }

    public static bool CheckConditions(int type)
    {
        ModNPC? own = GetNPC(type);
        if (!Has(own, NPCMethod.TownRoom)) return true;
        try { return own!.CheckConditions(WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2); }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CheckConditions", e); return true; }
    }

    public static List<string> ModifyNPCNameList(NPC npc, List<string> nameList)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if (!Has(global, NPCMethod.TownNames)) continue;
            try { if (global.AppliesToEntity(npc, false)) global.ModifyNPCNameList(npc, nameList); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyNPCNameList", e); }
        }
        return nameList;
    }

    public static bool? CanChat(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        bool? result = null;
        if (own != null)
        {
            try { result = own.CanChat(); }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.CanChat", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownChat)) continue;
            try
            {
                bool? value = global!.CanChat(npc);
                if (value == false) return false;
                if (value == true) result = true;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanChat", e); }
        }
        return result;
    }

    public static void GetChat(NPC npc, ref string chat)
    {
        ModNPC? own = InstanceOf(npc);
        if (own != null)
        {
            try { chat = own.GetChat(); }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.GetChat", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownChat)) continue;
            try { global!.GetChat(npc, ref chat); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.GetChat", e); }
        }
    }

    public static void RegisterChatButtons(NPC npc, NPCInteractionList interactions)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownButtons))
        {
            try { own!.RegisterChatButtons(interactions); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.RegisterChatButtons", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownButtons)) continue;
            try { global!.RegisterChatButtons(npc, interactions); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.RegisterChatButtons", e); }
        }
    }

    public static bool PreChatButtonClicked(NPCInteraction interaction)
    {
        if (interaction.TalkNPC is not NPC npc || npc.IsNull) return true;
        bool result = true;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownButtons)) continue;
            try { result &= global!.PreChatButtonClicked(npc, interaction); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PreChatButtonClicked", e); }
        }
        return result;
    }

    public static void OnChatButtonClicked(NPCInteraction interaction)
    {
        if (interaction.TalkNPC is not NPC npc || npc.IsNull) return;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownButtons))
        {
            try { own!.OnChatButtonClicked(interaction); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnChatButtonClicked", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownButtons)) continue;
            try { global!.OnChatButtonClicked(npc, interaction); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnChatButtonClicked", e); }
        }
    }

    public static void AddShops(int type)
    {
        ModNPC? own = GetNPC(type);
        if (!Has(own, NPCMethod.TownShops)) return;
        try { own!.AddShops(); }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.AddShops", e); }
    }

    public static void ModifyShop(NPCShop shop)
    {
        NPC sample = GetNPC(shop.NpcType)?.NPC ?? default;
        if (sample.IsNull) NPCLootHooks.TryGetSample(shop.NpcType, out sample);
        foreach (GlobalNPC global in Globals.Templates)
        {
            if (!Has(global, NPCMethod.TownShops)) continue;
            try { if (sample.IsNull || global.AppliesToEntity(sample, false)) global.ModifyShop(shop); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyShop", e); }
        }
    }

    public static void ModifyActiveShop(NPC npc, string shopName, Item[] items)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownShops))
        {
            try { own!.ModifyActiveShop(shopName, items); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyActiveShop", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownShops)) continue;
            try { global!.ModifyActiveShop(npc, shopName, items); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyActiveShop", e); }
        }
    }

    public static void SetupTravelShop(int[] shop, ref int nextSlot)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if (!Has(global, NPCMethod.TownShops)) continue;
            try { global.SetupTravelShop(shop, ref nextSlot); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.SetupTravelShop", e); }
        }
    }
}
