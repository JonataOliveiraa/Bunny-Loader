using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    static bool Has(ModNPC? npc, NPCHook hook) => npc != null && (npc.Extra & hook) != 0;
    static bool Has(GlobalNPC? global, NPCHook hook) => global != null && (global.Extra & hook) != 0;

    internal static bool HasHooks(NPC npc, NPCHook hooks)
    {
        if (Has(NPCEventHooks.Borrowed(npc) ?? GetNPC(npc.type), hooks)) return true;
        foreach (GlobalNPC? global in Slots(npc))
            if (Has(global, hooks)) return true;
        return false;
    }

    internal static bool AnyGlobal(NPCHook hook)
    {
        foreach (GlobalNPC global in Globals.Templates)
            if ((global.Extra & hook) != 0) return true;
        return false;
    }

    public static bool? CanFallThroughPlatforms(NPC npc)
    {
        bool? result = null;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.CanFallThroughPlatforms))
        {
            try
            {
                bool? value = own!.CanFallThroughPlatforms();
                if (value == false) return false;
                if (value == true) result = true;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanFallThroughPlatforms", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.CanFallThroughPlatforms)) continue;
            try
            {
                bool? value = global!.CanFallThroughPlatforms(npc);
                if (value == false) return false;
                if (value == true) result = true;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanFallThroughPlatforms", e); }
        }
        return result;
    }

    public static bool ModifyCollisionData(NPC npc, Rectangle victimHitbox, ref int immunityCooldownSlot, ref float damageMultiplier, ref Rectangle npcHitbox)
    {
        MultipliableFloat damageMult = MultipliableFloat.One;
        bool result = true;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.ModifyCollisionData)) continue;
            try { result &= global!.ModifyCollisionData(npc, victimHitbox, ref immunityCooldownSlot, ref damageMult, ref npcHitbox); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyCollisionData", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (result && Has(own, NPCHook.ModifyCollisionData))
        {
            try { result = own!.ModifyCollisionData(victimHitbox, ref immunityCooldownSlot, ref damageMult, ref npcHitbox); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyCollisionData", e); }
        }
        damageMultiplier *= damageMult.Value;
        return result;
    }

    public static bool SpecialOnKill(NPC npc)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.SpecialOnKill)) continue;
            try
            {
                if (global!.SpecialOnKill(npc)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.SpecialOnKill", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCHook.SpecialOnKill)) return false;
        try { return own!.SpecialOnKill(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.SpecialOnKill", e);
            return false;
        }
    }

    public static bool ModifyDeathMessage(NPC npc, ref NetworkText customText, ref Color color)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Extra & NPCHook.ModifyDeathMessage) == 0) continue;
            try
            {
                if (!global.ModifyDeathMessage(npc, ref customText, ref color)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyDeathMessage", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCHook.ModifyDeathMessage)) return false;
        try { return !own!.ModifyDeathMessage(ref customText, ref color); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.ModifyDeathMessage", e);
            return false;
        }
    }

    public static string ModifyTypeName(NPC npc, string typeName)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.ModifyTypeName))
        {
            try { own!.ModifyTypeName(ref typeName); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyTypeName", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.ModifyTypeName)) continue;
            try { global!.ModifyTypeName(npc, ref typeName); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyTypeName", e); }
        }
        return typeName;
    }

    public static bool? CanGoToStatue(NPC npc, bool toKingStatue)
    {
        bool? result = null;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.CanGoToStatue))
        {
            try { result = own!.CanGoToStatue(toKingStatue); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanGoToStatue", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.CanGoToStatue)) continue;
            try
            {
                if (global!.CanGoToStatue(npc, toKingStatue) is bool canGo)
                {
                    if (!canGo) return false;
                    result = true;
                }
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanGoToStatue", e); }
        }
        return result;
    }

    public static void OnGoToStatue(NPC npc, bool toKingStatue)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.OnGoToStatue))
        {
            try { own!.OnGoToStatue(toKingStatue); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnGoToStatue", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.OnGoToStatue)) continue;
            try { global!.OnGoToStatue(npc, toKingStatue); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnGoToStatue", e); }
        }
    }

    public static bool? CanBeCaughtBy(NPC npc, Item item, Player player)
    {
        bool? result = null;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.CanBeCaughtBy)) continue;
            try
            {
                bool? value = global!.CanBeCaughtBy(npc, item, player);
                if (value == false) return false;
                if (value == true) result = true;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanBeCaughtBy", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.CanBeCaughtBy))
        {
            try
            {
                bool? value = own!.CanBeCaughtBy(item, player);
                if (value == false) return false;
                if (value == true) result = true;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanBeCaughtBy", e); }
        }
        return result;
    }

    public static void OnCaughtBy(NPC npc, Player player, Item item, bool failed)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.OnCaughtBy))
        {
            try { own!.OnCaughtBy(player, item, failed); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnCaughtBy", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.OnCaughtBy)) continue;
            try { global!.OnCaughtBy(npc, player, item, failed); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnCaughtBy", e); }
        }
    }

    public static bool UsesPartyHat(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCHook.UsesPartyHat)) return true;
        try { return own!.UsesPartyHat(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.UsesPartyHat", e);
            return true;
        }
    }

    public static void BuffTownNPC(ref float damageMult, ref int defense)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Extra & NPCHook.BuffTownNPC) == 0) continue;
            try { global.BuffTownNPC(ref damageMult, ref defense); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.BuffTownNPC", e); }
        }
    }

    public static void PartyHatPosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.PartyHatPosition))
        {
            try { own!.PartyHatPosition(ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PartyHatPosition", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.PartyHatPosition)) continue;
            try { global!.PartyHatPosition(npc, ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PartyHatPosition", e); }
        }
    }

    public static void ChatBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.ChatBubblePosition))
        {
            try { own!.ChatBubblePosition(ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ChatBubblePosition", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.ChatBubblePosition)) continue;
            try { global!.ChatBubblePosition(npc, ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ChatBubblePosition", e); }
        }
    }

    public static void ModifyHoverBoundingBox(NPC npc, ref Rectangle boundingBox)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.ModifyHoverBoundingBox))
        {
            try { own!.ModifyHoverBoundingBox(ref boundingBox); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyHoverBoundingBox", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.ModifyHoverBoundingBox)) continue;
            try { global!.ModifyHoverBoundingBox(npc, ref boundingBox); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyHoverBoundingBox", e); }
        }
    }

    public static bool PreHoverInteract(NPC npc, bool mouseIntersects)
    {
        bool result = true;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.PreHoverInteract)) continue;
            try { result &= global!.PreHoverInteract(npc, mouseIntersects); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PreHoverInteract", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!result || !Has(own, NPCHook.PreHoverInteract)) return result;
        try { return own!.PreHoverInteract(mouseIntersects); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.PreHoverInteract", e);
            return true;
        }
    }

    public static bool NeedSaving(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.NeedSaving))
        {
            try
            {
                if (own!.NeedSaving()) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.NeedSaving", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.NeedSaving)) continue;
            try
            {
                if (global!.NeedSaving(npc)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.NeedSaving", e); }
        }
        return false;
    }

    public static ITownNPCProfile? TownNPCProfile(NPC npc)
    {
        ITownNPCProfile? profile = null;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.TownNPCProfile))
        {
            try { profile = own!.TownNPCProfile(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCProfile", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.ModifyTownNPCProfile)) continue;
            try { profile = global!.ModifyTownNPCProfile(npc) ?? profile; }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyTownNPCProfile", e); }
        }
        return profile;
    }

    internal static string SaveText(NPC npc)
    {
        var saved = new TagCompound();
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.SaveData))
        {
            var tag = new TagCompound();
            try { own!.SaveData(tag); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.SaveData", e); }
            if (tag.Count > 0) saved["data"] = tag;
        }
        var globals = new List<object>();
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.SaveData)) continue;
            var tag = new TagCompound();
            try { global!.SaveData(npc, tag); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.SaveData", e); }
            if (tag.Count == 0) continue;
            globals.Add(new TagCompound { ["mod"] = global!.Mod.Name, ["name"] = global.Name, ["data"] = tag });
        }
        if (globals.Count > 0) saved["globalData"] = globals;
        return saved.Count == 0 ? "" : TagJson.Write(saved);
    }

    internal static void LoadText(NPC npc, string text)
    {
        if (text.Length == 0) return;
        TagCompound saved = TagJson.Read(text);
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCHook.LoadData) && saved.TryGet("data", out TagCompound data))
        {
            try { own!.LoadData(data); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.LoadData", e); }
        }
        if (!saved.TryGet("globalData", out List<object> globals)) return;
        foreach (object entry in globals)
        {
            if (entry is not TagCompound tag || !tag.TryGet("data", out TagCompound globalData)) continue;
            string mod = tag.GetString("mod"), name = tag.GetString("name");
            foreach (GlobalNPC? global in Slots(npc))
            {
                if (!Has(global, NPCHook.LoadData) || global!.Mod.Name != mod || global.Name != name) continue;
                try { global.LoadData(npc, globalData); }
                catch (Exception e) { ItemHooks.Report($"{global.FullName}.LoadData", e); }
                break;
            }
        }
    }

    public static bool? DrawHealthBar(NPC npc, byte hbPosition, ref float scale, ref Vector2 position)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCHook.DrawHealthBar)) continue;
            try
            {
                if (global!.DrawHealthBar(npc, hbPosition, ref scale, ref position) is bool result) return result;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.DrawHealthBar", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCHook.DrawHealthBar)) return null;
        try { return own!.DrawHealthBar(hbPosition, ref scale, ref position); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.DrawHealthBar", e);
            return null;
        }
    }
}
