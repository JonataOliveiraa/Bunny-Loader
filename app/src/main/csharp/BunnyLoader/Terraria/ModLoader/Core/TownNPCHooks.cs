using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static class TownNPCHooks
{
    static readonly Dictionary<int, NPCInteractionList> Interactions = new();

    internal static void Install(Type type, Type baseType, int npcType, ref NPCMethod flags)
    {
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        bool Any(params string[] methods) { foreach (string method in methods) if (Has(method)) return true; return false; }
        if (Has(nameof(ModNPC.CanTownNPCSpawn)))
        {
            flags |= NPCMethod.TownSpawn;
            ItemHooks.Once("TownSpawn", () => On_Main.UpdateTime_SpawnTownNPCs += Spawn);
        }
        if (Has(nameof(ModNPC.CheckConditions)))
        {
            flags |= NPCMethod.TownRoom;
            ItemHooks.Once("TownRoom", () => On_WorldGen.CheckSpecialTownNPCSpawningConditions += Conditions);
        }
        if (Any(nameof(ModNPC.SetNPCNameList), nameof(GlobalNPC.ModifyNPCNameList)))
        {
            flags |= NPCMethod.TownNames;
            ItemHooks.Once("TownNames", () => On_NPC.getNewNPCName += Name);
        }
        if (Any(nameof(ModNPC.CanChat), nameof(ModNPC.GetChat)))
        {
            flags |= NPCMethod.TownChat;
            if (Has(nameof(ModNPC.GetChat))) ItemHooks.Once("TownChat", () => On_NPC.GetChat += Chat);
        }
        if (Any(nameof(ModNPC.RegisterChatButtons), nameof(ModNPC.SetChatButtons), nameof(ModNPC.OnChatButtonClicked), nameof(GlobalNPC.PreChatButtonClicked)))
        {
            flags |= NPCMethod.TownButtons;
            ItemHooks.Once("TownButtons", () =>
            {
                On_GUINPCDialogue.SetupButtonText += Buttons;
                On_GUINPCDialogue.Option1Clicked += First;
                On_GUINPCDialogue.Option2Clicked += Second;
            });
        }
        if (Any(nameof(ModNPC.AddShops), nameof(ModNPC.ModifyActiveShop), nameof(GlobalNPC.ModifyShop), nameof(GlobalNPC.SetupTravelShop)))
        {
            flags |= NPCMethod.TownShops;
            TownShopHooks.Install();
            if (Has(nameof(GlobalNPC.SetupTravelShop))) TownShopHooks.InstallTravel();
        }
        if (Any(nameof(ModNPC.TownNPCAttackStrength), nameof(ModNPC.TownNPCAttackCooldown), nameof(ModNPC.TownNPCAttackProj), nameof(ModNPC.TownNPCAttackProjSpeed),
            nameof(ModNPC.TownNPCAttackShoot), nameof(ModNPC.TownNPCAttackMagic), nameof(ModNPC.TownNPCAttackSwing))) flags |= NPCMethod.TownAttack;
        if (Any(nameof(ModNPC.DrawTownAttackGun), nameof(ModNPC.DrawTownAttackSwing))) flags |= NPCMethod.TownAttackDraw;
        TownAttackHooks.Install(npcType, flags, Has(nameof(ModNPC.CanChat)));
    }

    internal static NPC TalkingTo()
    {
        Player player = Main.LocalPlayer;
        if (player.IsNull || (uint)player.talkNPC >= 200u) return default;
        NPC npc = Main.npc[player.talkNPC];
        return npc.IsNull || !npc.active ? default : npc;
    }

    static void Spawn(On_Main.orig_UpdateTime_SpawnTownNPCs orig)
    {
        orig();
        if (Main.netMode == 1 || Main.checkForSpawns != 0) return;
        int count = 0;
        for (int i = 0; i < 200; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.IsNull && npc.active && npc.townNPC && npc.type != 368) count++;
        }
        NPCLoader.CanTownNPCSpawn(count);
    }

    static bool Conditions(On_WorldGen.orig_CheckSpecialTownNPCSpawningConditions orig, int type) =>
        NPCLoader.GetNPC(type) == null ? orig(type) : NPCLoader.CheckConditions(type);

    static string? Name(On_NPC.orig_getNewNPCName orig, int type)
    {
        ModNPC? own = NPCLoader.GetNPC(type);
        List<string> names = new();
        try { if (own != null) names = own.SetNPCNameList() ?? names; }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.SetNPCNameList", e); }
        string? fallback = names.Count == 0 ? orig(type) : null;
        if (fallback != null) names.Add(fallback);
        NPC npc = own?.NPC ?? NPC.New();
        using GameHandle pin = new(npc.Pointer);
        if (own == null) npc.SetDefaults(type, default);
        NPCLoader.ModifyNPCNameList(npc, names);
        names.RemoveAll(string.IsNullOrEmpty);
        return names.Count == 0 ? fallback ?? orig(type) : names[Main.rand.Next(names.Count)];
    }

    static string? Chat(On_NPC.orig_GetChat orig, NPC npc)
    {
        string chat = orig(npc) ?? "";
        NPCLoader.GetChat(npc, ref chat);
        return chat;
    }

    static NPCInteractionList List(NPC npc)
    {
        if (Interactions.TryGetValue(npc.type, out NPCInteractionList? list)) return list;
        list = new(npc.type);
        Interactions.Add(npc.type, list);
        ModNPC? own = NPCLoader.InstanceOf(npc);
        if (own != null && ItemHooks.Overrides(own.GetType(), typeof(ModNPC), nameof(ModNPC.SetChatButtons)))
        {
            list.Append(new LegacyButton(true));
            list.Append(new LegacyButton(false));
        }
        list.Append(NPCInteractionDatabase.CloseButton);
        list.Append(NPCInteractionDatabase.HappinessButton);
        list.Append(NPCInteractionDatabase.HousingButton);
        NPCLoader.RegisterChatButtons(npc, list);
        return list;
    }

    static NPCInteraction? Choice(NPC npc, bool first)
    {
        int wanted = first ? 0 : 1;
        foreach (NPCInteractionList.Entry entry in List(npc).Entries)
        {
            if (!entry.Enabled || entry.NPCInteraction is NPCInteractionDatabase.DedicatedButton) continue;
            try
            {
                if (!entry.NPCInteraction.Condition() || string.IsNullOrEmpty(entry.NPCInteraction.GetText())) continue;
                if (wanted-- == 0) return entry.NPCInteraction;
            }
            catch (Exception e) { ItemHooks.Report("NPC interaction", e); }
        }
        return null;
    }

    static void Buttons(On_GUINPCDialogue.orig_SetupButtonText orig, GUINPCDialogue self, ref GameString text, ref Texture2D texture,
        ref GameString text2, ref Texture2D texture2, ref int cost, ref bool happiness)
    {
        NPC npc = TalkingTo();
        bool custom = !npc.IsNull && NPCLoader.GetNPC(npc.type) != null;
        int type = npc.IsNull ? 0 : npc.type;
        if (custom) npc.type = 17;
        try { orig(self, ref text, ref texture, ref text2, ref texture2, ref cost, ref happiness); }
        finally { if (custom) npc.type = type; }
        if (npc.IsNull) return;
        var list = List(npc);
        bool hasCustom = false;
        foreach (NPCInteractionList.Entry entry in list.Entries) if (entry.Enabled && entry.NPCInteraction is not NPCInteractionDatabase.DedicatedButton) { hasCustom = true; break; }
        if (!hasCustom && !custom) return;
        NPCInteraction? first = Choice(npc, true), second = Choice(npc, false);
        Texture2D icon = texture;
        text = first?.GetText() ?? "";
        texture = first == null ? default : icon;
        text2 = second?.GetText() ?? "";
        texture2 = second == null ? default : icon;
        cost = 0;
    }

    static void Click(NPC npc, bool first, Action original)
    {
        NPCInteraction interaction = Choice(npc, first) ?? new NativeButton(first, original);
        if (!NPCLoader.PreChatButtonClicked(interaction)) return;
        try { interaction.Interact(); }
        catch (Exception e) { ItemHooks.Report("NPC interaction", e); }
        NPCLoader.OnChatButtonClicked(interaction);
    }

    static void First(On_GUINPCDialogue.orig_Option1Clicked orig, GUINPCDialogue self, int healCost)
    {
        NPC npc = TalkingTo();
        if (npc.IsNull) { orig(self, healCost); return; }
        Click(npc, true, () => orig(self, healCost));
    }

    static void Second(On_GUINPCDialogue.orig_Option2Clicked orig, GUINPCDialogue self)
    {
        NPC npc = TalkingTo();
        if (npc.IsNull) { orig(self); return; }
        Click(npc, false, () => orig(self));
    }

    sealed class NativeButton(bool first, Action action) : NPCInteraction
    {
        public bool FirstButton => first;
        public override bool Condition() => true;
        public override string GetText() => "";
        public override void Interact() => action();
    }

    sealed class LegacyButton(bool first) : NPCInteraction
    {
        public override bool Condition() => !string.IsNullOrEmpty(GetText());
        public override string GetText()
        {
            NPC npc = TalkingTo();
            ModNPC? own = NPCLoader.InstanceOf(npc);
            if (own == null) return "";
            string button = "", button2 = "";
            own.SetChatButtons(ref button, ref button2);
            return first ? button : button2;
        }
        public override void Interact()
        {
            NPC npc = TalkingTo();
            ModNPC? own = NPCLoader.InstanceOf(npc);
            if (own == null) return;
            string shopName = "";
            own.OnChatButtonClicked(first, ref shopName);
            if (!string.IsNullOrEmpty(shopName)) NPCShopDatabase.OpenShop(NPCShopDatabase.GetShopName(npc.type, shopName));
        }
    }
}
