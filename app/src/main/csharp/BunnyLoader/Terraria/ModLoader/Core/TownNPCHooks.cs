using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class TownNPCHooks
{
    static readonly Dictionary<int, NPCInteractionList> Interactions = new();
    static string vanillaFirst = "", vanillaSecond = "";
    static GUINPCDialogue dialogue;
    static int healCost;
    static bool passThrough;
    const int OptionsPerPage = 2;
    const float PageButtonRoom = 46f;
    const float PageButtonInset = 48f;
    const float PageIconCell = 2f;
    static int chatPage;
    static nint chatNpc, widenedLayout;
    static bool pageButton, widened;
    static float pageScale = 1f;
    static readonly string[] PageIcon =
    {
        ".......#...",
        ".......##..",
        "##########.",
        ".......##..",
        ".......#...",
        "...........",
        "...#.......",
        "..##.......",
        ".##########",
        "..##.......",
        "...#.......",
    };

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
                On_GUINPCDialogue.CloseClicked += CloseClicked;
                On_GUINPCDialogue.Draw += DrawDialogue;
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
        else if (own == null)
        {
            if (npc.type == NPCID.Guide)
            {
                list.Append(new NPCInteractions.Actions.GuideTip());
                list.Append(new NPCInteractions.Actions.GuideReverseCrafting());
            }
            else
            {
                list.Append(new VanillaButton(true));
                list.Append(new VanillaButton(false));
            }
        }
        list.Append(NPCInteractionDatabase.CloseButton);
        list.Append(NPCInteractionDatabase.HappinessButton);
        list.Append(NPCInteractionDatabase.HousingButton);
        NPCLoader.RegisterChatButtons(npc, list);
        return list;
    }

    internal static string VanillaText(bool first) => first ? vanillaFirst : vanillaSecond;

    internal static void VanillaClick(bool first)
    {
        passThrough = true;
        try
        {
            if (first) dialogue.Option1Clicked(healCost);
            else dialogue.Option2Clicked();
        }
        finally { passThrough = false; }
    }

    static bool Customized(NPCInteractionList list)
    {
        foreach (NPCInteractionList.Entry entry in list.Entries)
        {
            if (entry.NPCInteraction is NPCInteractions.Actions.VanillaOption ? !entry.Enabled : entry.Enabled && entry.NPCInteraction is not NPCInteractionDatabase.DedicatedButton) return true;
        }
        return false;
    }

    static List<NPCInteraction> Shown(NPC npc)
    {
        var shown = new List<NPCInteraction>();
        foreach (NPCInteractionList.Entry entry in List(npc).Entries)
        {
            if (!entry.Enabled || entry.NPCInteraction is NPCInteractionDatabase.DedicatedButton) continue;
            try
            {
                if (entry.NPCInteraction.Condition() && !string.IsNullOrEmpty(entry.NPCInteraction.GetText())) shown.Add(entry.NPCInteraction);
            }
            catch (Exception e) { ItemHooks.Report("NPC interaction", e); }
        }
        return shown;
    }

    static NPCInteraction? Choice(NPC npc, bool first)
    {
        if (npc.Pointer != chatNpc)
        {
            chatNpc = npc.Pointer;
            chatPage = 0;
        }
        List<NPCInteraction> shown = Shown(npc);
        int pages = (shown.Count + OptionsPerPage - 1) / OptionsPerPage;
        if (chatPage >= pages) chatPage = 0;
        int index = chatPage * OptionsPerPage + (first ? 0 : 1);
        return index < shown.Count ? shown[index] : null;
    }

    static int ChatPages(NPC npc) => (Shown(npc).Count + OptionsPerPage - 1) / OptionsPerPage;

    static void DrawDialogue(On_GUINPCDialogue.orig_Draw orig, GUINPCDialogue self)
    {
        NPC npc = TalkingTo();
        bool paged = false;
        try { paged = !npc.IsNull && (Customized(List(npc)) || NPCLoader.GetNPC(npc.type) != null) && ChatPages(npc) > 1; }
        catch (Exception e) { ItemHooks.Report("NPC chat pages", e); }
        NPCDialogue_Layout layout = NPCDialogue_Layout.Instance;
        if (layout.IsNull)
        {
            orig(self);
            return;
        }
        Widen(layout, paged);
        pageButton = paged;
        try { orig(self); }
        finally { pageButton = false; }
    }

    static void Widen(NPCDialogue_Layout layout, bool wide)
    {
        if (layout.Pointer != widenedLayout)
        {
            widenedLayout = layout.Pointer;
            widened = false;
        }
        if (wide == widened) return;
        widened = wide;
        float room = wide ? PageButtonRoom : -PageButtonRoom;
        layout.Backing.Size.X += room;
        layout.Option1.Location.X += room;
        layout.Option1Cost.Location.X += room;
        layout.MenuDivider.Size.X += room;
        layout.TextBacking.Size.X += room;
        layout.Text.Location.X += room;
        layout.Text.WrapWidth += room / layout.Text.Scale;
        layout.DialogueItemName.Location.X += room;
        layout.DialogueItemNameDivider.Size.X += room;
        layout.DialogueItemNameDivider.Location.X += room / 2f;
    }

    static void DrawPageButton(NPC npc)
    {
        NPCDialogue_Layout layout = NPCDialogue_Layout.Instance;
        TransactionButton_Layout button = layout.Happiness;
        if (button.IsNull) return;
        Vector2 location = button.Location;
        button.Location = new Vector2(layout.Backing.Size.X - PageButtonInset, location.Y);
        try
        {
            var state = GUITransactionButton.Draw(button, TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 1, 1), "", false, ref pageScale, new Color(0, 0, 0, 0));
            DrawPageIcon(GUITransactionButton.GetRegion(button), ChatPages(npc));
            if (state != GUITransactionButton.InputState.Clicked) return;
            chatPage = (chatPage + 1) % Math.Max(1, ChatPages(npc));
            Terraria.Audio.SoundEngine.PlaySound(12);
        }
        finally { button.Location = location; }
    }

    static void DrawPageIcon(Rectangle region, int pages)
    {
        int columns = PageIcon[0].Length, rows = PageIcon.Length;
        float cell = PageIconCell * pageScale;
        float x = region.Center.X - columns * cell / 2f, y = region.Center.Y - (rows + 2) * cell / 2f;
        Color outline = new(16, 18, 58), fill = new(240, 240, 255), idle = new(112, 122, 186);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (PageIcon[row][column] != '#') continue;
                    float left = x + column * cell, top = y + row * cell;
                    if (pass == 0) PageCell(left - cell, top - cell, cell * 3f, outline);
                    else PageCell(left, top, cell, fill);
                }
            }
        }
        float dotsLeft = region.Center.X - (pages * 2 - 1) * cell / 2f, dotsTop = y + (rows + 1) * cell;
        for (int page = 0; page < pages; page++) PageCell(dotsLeft + page * 2 * cell, dotsTop, cell, page == chatPage ? fill : idle);
    }

    static void PageCell(float x, float y, float size, Color color) =>
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(x, y), new Rectangle(0, 0, 1, 1), color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);

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
        vanillaFirst = text!;
        vanillaSecond = text2!;
        var list = List(npc);
        if (!Customized(list) && !custom) return;
        NPCInteraction? first = Choice(npc, true), second = Choice(npc, false);
        if (pageButton)
        {
            try { DrawPageButton(npc); }
            catch (Exception e) { ItemHooks.Report("NPC chat page button", e); }
            first = Choice(npc, true);
            second = Choice(npc, false);
        }
        Texture2D icon = texture;
        text = first?.GetText() ?? "";
        texture = first == null ? default : icon;
        text2 = second?.GetText() ?? "";
        texture2 = second == null ? default : icon;
        if (first is not NPCInteractions.Actions.VanillaOption { First: true }) cost = 0;
    }

    static void Click(NPC npc, bool first, Action original)
    {
        NPCInteractionList list = List(npc);
        NPCInteraction? chosen = Customized(list) || NPCLoader.GetNPC(npc.type) != null
            ? Choice(npc, first)
            : list.Interactions.OfType<NPCInteractions.Actions.VanillaOption>().FirstOrDefault(option => option.First == first);
        NPCInteraction interaction = chosen ?? new NativeButton(first, original);
        if (!NPCLoader.PreChatButtonClicked(interaction)) return;
        try { interaction.Interact(); }
        catch (Exception e) { ItemHooks.Report("NPC interaction", e); }
        NPCLoader.OnChatButtonClicked(interaction);
    }

    static void First(On_GUINPCDialogue.orig_Option1Clicked orig, GUINPCDialogue self, int healCost)
    {
        NPC npc = TalkingTo();
        if (npc.IsNull || passThrough) { orig(self, healCost); return; }
        dialogue = self;
        TownNPCHooks.healCost = healCost;
        Click(npc, true, () => orig(self, healCost));
    }

    static void Second(On_GUINPCDialogue.orig_Option2Clicked orig, GUINPCDialogue self)
    {
        NPC npc = TalkingTo();
        if (npc.IsNull || passThrough) { orig(self); return; }
        dialogue = self;
        Click(npc, false, () => orig(self));
    }

    static void CloseClicked(On_GUINPCDialogue.orig_CloseClicked orig, GUINPCDialogue self)
    {
        NPC npc = TalkingTo();
        if (npc.IsNull) { orig(self); return; }
        if (!NPCLoader.PreChatButtonClicked(NPCInteractionDatabase.CloseButton)) return;
        NPCLoader.OnChatButtonClicked(NPCInteractionDatabase.CloseButton);
        orig(self);
    }

    sealed class VanillaButton(bool first) : NPCInteractions.Actions.VanillaOption(first);

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
