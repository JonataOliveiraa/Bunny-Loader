using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemEquipHooks
{
    static readonly object Gate = new();

    static readonly Dictionary<int, (Item Item, GameHandle Pin)> Samples = new();

    static Hook? setMatch;

    static readonly EntityInstances<string> SetBonuses = new();

    static readonly string?[] BonusText = new string?[256];

    static readonly IntPtr[] BonusOwner = new IntPtr[256];

    static volatile bool setBonusWanted;

    internal static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.UpdateEquip)) || has(nameof(ModItem.UpdateInfoAccessory)))
            ItemHooks.Once("EquipArmor", () =>
            {
                ItemHooks.TypeGate<On_Player.hook_GrantArmorBenefits>(1, static type =>
                    ItemHooks.WantsType(type, nameof(ModItem.UpdateEquip)) || ItemHooks.WantsType(type, nameof(ModItem.UpdateInfoAccessory)));
                On_Player.GrantArmorBenefits += GrantArmorBenefits;
            });
        if (has(nameof(ModItem.UpdateAccessory)))
            ItemHooks.Once("EquipFunctional", () =>
            {
                ItemHooks.TypeGate<On_Player.hook_ApplyEquipFunctional>(2, static type => ItemHooks.WantsType(type, nameof(ModItem.UpdateAccessory)));
                On_Player.ApplyEquipFunctional += ApplyEquipFunctional;
            });
        if (has(nameof(ModItem.UpdateVanity)))
            ItemHooks.Once("EquipVanity", () =>
            {
                ItemHooks.TypeGate<On_Player.hook_ApplyEquipVanity>(2, static type => ItemHooks.WantsType(type, nameof(ModItem.UpdateVanity)));
                On_Player.ApplyEquipVanity += ApplyEquipVanity;
            });
        if (has(nameof(ModItem.CanAccessoryBeEquippedWith))) ItemHooks.Once("AccessoryPairs", () => On_ItemSlot.CanEquipBothAccessories += CanEquipBothAccessories);
        if (has(nameof(ModItem.CanEquipAccessory)))
            ItemHooks.Once("AccessorySlots", () =>
            {
                On_ItemSlot.PickItemMovementAction += PickItemMovementAction;
                On_ItemSlot.ArmorSwap += ArmorSwap;
                On_ItemSlot.CanEquipAccessoryInSlot += CanEquipAccessoryInSlot;
                On_ItemSlot.CanEquipAccessoryInVanitySlot += CanEquipAccessoryInVanitySlot;
            });
        if (has(nameof(ModItem.IsArmorSet)) || has(nameof(ModItem.UpdateArmorSet))) WantArmorSets();
        if (has(nameof(ModItem.IsVanitySet)) || has(nameof(ModItem.PreUpdateVanitySet)) || has(nameof(ModItem.UpdateVanitySet)) ||
            has(nameof(ModItem.EquipFrameEffects)))
            WantFrame();
        if (has(nameof(ModItem.IsVanitySet)) || has(nameof(ModItem.ArmorSetShadows))) WantShadows();
        if (has(nameof(ModItem.SetMatch))) WantSetMatch();
        ItemArmorDrawHooks.Install(has);
        ItemWingHooks.Install(has, global);
    }

    internal static void WantTexture(Type type)
    {
        if (type == typeof(EquipTexture)) return;
        bool Has(string name) => ItemHooks.Overrides(type, typeof(EquipTexture), name);
        if (Has(nameof(EquipTexture.IsVanitySet)) || Has(nameof(EquipTexture.PreUpdateVanitySet)) || Has(nameof(EquipTexture.UpdateVanitySet)) ||
            Has(nameof(EquipTexture.FrameEffects)))
            WantFrame();
        if (Has(nameof(EquipTexture.IsVanitySet)) || Has(nameof(EquipTexture.ArmorSetShadows))) WantShadows();
        if (Has(nameof(EquipTexture.SetMatch))) WantSetMatch();
        if (Has(nameof(EquipTexture.DrawArmorColor)) || Has(nameof(EquipTexture.ArmorArmGlowMask))) ItemArmorDrawHooks.WantColors();
        if (Has(nameof(EquipTexture.VerticalWingSpeeds))) ItemWingHooks.Want(ItemWingHooks.Vertical, false);
        if (Has(nameof(EquipTexture.HorizontalWingSpeeds))) ItemWingHooks.Want(ItemWingHooks.Horizontal, false);
        if (Has(nameof(EquipTexture.WingUpdate))) ItemWingHooks.Want(ItemWingHooks.Update, false);
    }

    internal static void WantArmorSets() => ItemHooks.Once("ArmorSets", () => On_Player.UpdateArmorSets += UpdateArmorSets);

    internal static string GetSetBonus(Player player)
    {
        if (player.IsNull) return "";
        if (!Listed(player, out uint who)) return SetBonuses.Get(player.Pointer) ?? "";
        return BonusOwner[who] == player.Pointer ? BonusText[who] ?? "" : "";
    }

    internal static void SetSetBonus(Player player, string? text)
    {
        if (!setBonusWanted)
        {
            setBonusWanted = true;
            WantArmorSets();
            ItemTooltipHooks.WantSetBonusLines();
        }
        StoreSetBonus(player, string.IsNullOrEmpty(text) ? null : text);
    }

    static void StoreSetBonus(Player player, string? text)
    {
        if (player.IsNull) return;
        if (!Listed(player, out uint who))
        {
            SetBonuses.Set(player.Pointer, text);
            return;
        }
        BonusText[who] = text;
        BonusOwner[who] = player.Pointer;
    }

    static bool Listed(Player player, out uint who)
    {
        who = (uint)player.whoAmI;
        if (who >= (uint)BonusText.Length) return false;
        var players = Main.player;
        return !players.IsNull && who < (uint)players.Length && players[(int)who].Pointer == player.Pointer;
    }

    internal static void WantFrame() => ItemHooks.Once("ArmorFrame", () => On_Player.PlayerFrame += PlayerFrame);

    static void WantShadows() => ItemHooks.Once("ArmorShadows", () => On_Player.SetArmorEffectVisuals += SetArmorEffectVisuals);

    static void WantSetMatch() => ItemHooks.Once("ArmorSetMatch", () => setMatch = ItemHooks.Raw("Terraria", "Player",
        "int SetMatch(Player.SetMatchRequest request, ref bool somethingSpecial)",
        (IntPtr)(delegate* unmanaged<Player.SetMatchRequest*, byte*, IntPtr, int>)&SetMatch));

    internal static Item Sample(GameArray<int> table, int slot)
    {
        int type = !table.IsNull && slot > 0 && slot < table.Length ? table[slot] : 0;
        lock (Gate)
        {
            if (Samples.TryGetValue(type, out var sample)) return sample.Item;
            Item item = Item.New();
            var pin = new GameHandle(item.Pointer);
            item.SetDefaults(type, default);
            Samples[type] = (item, pin);
            return item;
        }
    }

    static GlobalItem[] Globals => ItemLoader.Globals.Templates;

    static bool Wants(GlobalItem global, string name) => ItemHooks.Wants(global, name);

    static void ApplyEquipFunctional(On_Player.orig_ApplyEquipFunctional orig, Player self, int itemSlot, Item currentItem)
    {
        orig(self, itemSlot, currentItem);
        if (currentItem.IsNull) return;
        int type = currentItem.type;
        if (type <= 0) return;
        try
        {
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateAccessory));
            ModItem? m = ItemHooks.Overrider(currentItem, type, nameof(ModItem.UpdateAccessory));
            if ((m == null && overriding.Length == 0) || !currentItem.accessory) return;
            var hidden = self.hideVisibleAccessory;
            bool hide = !hidden.IsNull && itemSlot >= 0 && itemSlot < hidden.Length && hidden[itemSlot];
            if (m != null)
            {
                try { m.UpdateAccessory(self, hide); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.{nameof(ModItem.UpdateAccessory)}", e); }
            }
            if (overriding.Length == 0) return;
            GlobalItem?[]? slots = ItemLoader.Globals.Slots(currentItem);
            foreach (short index in overriding)
            {
                if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
                try { g.UpdateAccessory(currentItem, self, hide); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.UpdateAccessory)}", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("UpdateAccessory", e); }
    }

    static void GrantArmorBenefits(On_Player.orig_GrantArmorBenefits orig, Player self, Item armorPiece)
    {
        orig(self, armorPiece);
        if (armorPiece.IsNull) return;
        int type = armorPiece.type;
        if (type <= 0) return;
        try
        {
            ItemHooks.UpdateInfoAccessory(armorPiece, self);
            type = armorPiece.type;
            if (type <= 0 || armorPiece.stack <= 0) return;
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateEquip));
            if (ItemHooks.Overrider(armorPiece, type, nameof(ModItem.UpdateEquip)) is ModItem m)
            {
                try { m.UpdateEquip(self); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.{nameof(ModItem.UpdateEquip)}", e); }
            }
            if (overriding.Length == 0) return;
            GlobalItem?[]? slots = ItemLoader.Globals.Slots(armorPiece);
            foreach (short index in overriding)
            {
                if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
                try { g.UpdateEquip(armorPiece, self); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.UpdateEquip)}", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("UpdateEquip", e); }
    }

    static void ApplyEquipVanity(On_Player.orig_ApplyEquipVanity orig, Player self, int itemSlot, Item currentItem)
    {
        orig(self, itemSlot, currentItem);
        if (currentItem.IsNull) return;
        int type = currentItem.type;
        if (type <= 0) return;
        try
        {
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateVanity));
            if (ItemHooks.Overrider(currentItem, type, nameof(ModItem.UpdateVanity)) is ModItem m)
            {
                try { m.UpdateVanity(self); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.{nameof(ModItem.UpdateVanity)}", e); }
            }
            if (overriding.Length == 0) return;
            GlobalItem?[]? slots = ItemLoader.Globals.Slots(currentItem);
            foreach (short index in overriding)
            {
                if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
                try { g.UpdateVanity(currentItem, self); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.UpdateVanity)}", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("UpdateVanity", e); }
    }

    static bool CanEquipBothAccessories(On_ItemSlot.orig_CanEquipBothAccessories orig, Item acc1, Item acc2, bool vanity)
    {
        if (!orig(acc1, acc2, vanity)) return false;
        if (acc1.IsNull || acc2.IsNull || acc1.type <= 0 || acc2.type <= 0) return true;
        try
        {
            Player player = Main.LocalPlayer;
            return Pair(acc1, acc2, player) && Pair(acc2, acc1, player);
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanAccessoryBeEquippedWith", e);
            return true;
        }
    }

    static bool Pair(Item equipped, Item incoming, Player player)
    {
        foreach (Item item in new[] { equipped, incoming })
        {
            if (ItemHooks.ModItemOf(item) is not ModItem m) continue;
            bool ok = true;
            Run(m.FullName, nameof(ModItem.CanAccessoryBeEquippedWith), () => ok = m.CanAccessoryBeEquippedWith(equipped, incoming, player));
            if (!ok) return false;
        }
        foreach (GlobalItem g in Globals)
        {
            if (!Wants(g, nameof(GlobalItem.CanAccessoryBeEquippedWith))) continue;
            bool ok = true;
            Run(g.FullName, nameof(GlobalItem.CanAccessoryBeEquippedWith), () => ok = g.CanAccessoryBeEquippedWith(equipped, incoming, player));
            if (!ok) return false;
        }
        return true;
    }

    static int PickItemMovementAction(On_ItemSlot.orig_PickItemMovementAction orig, GameArray<Item> inv, int context, int slot, Item checkItem,
                                      bool backwardCheck, int initialSlot)
    {
        int action = orig(inv, context, slot, checkItem, backwardCheck, initialSlot);
        if (action == -1 || Math.Abs(context) is not (ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipAccessoryVanity)) return action;
        if (checkItem.IsNull || checkItem.type <= 0 || !checkItem.accessory) return action;
        return CanEquipAccessory(checkItem, slot, context < 0) ? action : -1;
    }

    static Item ArmorSwap(On_ItemSlot.orig_ArmorSwap orig, Item item, out bool success, out int targetSlot)
    {
        Item result = orig(item, out success, out targetSlot);
        if (!success || targetSlot < 0) return result;
        try
        {
            var armor = Main.LocalPlayer.armor;
            if (armor.IsNull || targetSlot >= armor.Length) return result;
            Item incoming = armor[targetSlot];
            if (incoming.IsNull || !incoming.accessory || CanEquipAccessory(incoming, targetSlot, false)) return result;
            armor[targetSlot] = result;
            success = false;
            targetSlot = -1;
            return incoming;
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanEquipAccessory", e);
            return result;
        }
    }

    static bool CanEquipAccessoryInSlot(On_ItemSlot.orig_CanEquipAccessoryInSlot orig, Item checkItem, int slot) =>
        orig(checkItem, slot) && (checkItem.IsNull || checkItem.type <= 0 || CanEquipAccessory(checkItem, slot, false));

    static bool CanEquipAccessoryInVanitySlot(On_ItemSlot.orig_CanEquipAccessoryInVanitySlot orig, Item checkItem, int slot) =>
        orig(checkItem, slot) && (checkItem.IsNull || checkItem.type <= 0 || CanEquipAccessory(checkItem, slot, false));

    internal static bool CanEquipAccessory(Item item, int slot, bool modded)
    {
        if (AccessorySlotHooks.ScopeSlot is int scopeSlot)
        {
            slot = scopeSlot;
            modded = true;
        }
        try
        {
            Player player = Main.LocalPlayer;
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                bool ok = true;
                Run(m.FullName, nameof(ModItem.CanEquipAccessory), () => ok = m.CanEquipAccessory(player, slot, modded));
                if (!ok) return false;
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null || !Wants(g, nameof(GlobalItem.CanEquipAccessory))) continue;
                bool ok = true;
                Run(g.FullName, nameof(GlobalItem.CanEquipAccessory), () => ok = g.CanEquipAccessory(item, player, slot, modded));
                if (!ok) return false;
            }
            return true;
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanEquipAccessory", e);
            return true;
        }
    }

    static void UpdateArmorSets(On_Player.orig_UpdateArmorSets orig, Player self, int i)
    {
        if (setBonusWanted) StoreSetBonus(self, null);
        orig(self, i);
        try
        {
            var armor = self.armor;
            Item head = armor[0], body = armor[1], legs = armor[2];
            ArmorSet(head, head, body, legs, self);
            ArmorSet(body, head, body, legs, self);
            ArmorSet(legs, head, body, legs, self);
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateArmorSet));
            if (overriding.Length > 0)
            {
                GlobalItem[] templates = Globals;
                foreach (short index in overriding)
                {
                    if (index >= templates.Length) continue;
                    GlobalItem g = templates[index];
                    string set;
                    try { set = g.IsArmorSet(head, body, legs) ?? ""; }
                    catch (Exception e)
                    {
                        ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.IsArmorSet)}", e);
                        continue;
                    }
                    if (set.Length == 0) continue;
                    try { g.UpdateArmorSet(self, set); }
                    catch (Exception e) { ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.UpdateArmorSet)}", e); }
                }
            }
        }
        catch (Exception e) { ItemHooks.Report("UpdateArmorSet", e); }
        PlayerLoader.Each(self, nameof(ModPlayer.PostUpdateEquips), m => m.PostUpdateEquips());
    }

    static void ArmorSet(Item piece, Item head, Item body, Item legs, Player player)
    {
        if (piece.IsNull) return;
        if (ItemHooks.Overrider(piece, piece.type, nameof(ModItem.IsArmorSet)) is not ModItem m) return;
        bool set;
        try { set = m.IsArmorSet(head, body, legs); }
        catch (Exception e)
        {
            ItemHooks.Report($"{m.FullName}.{nameof(ModItem.IsArmorSet)}", e);
            return;
        }
        if (!set) return;
        try { m.UpdateArmorSet(player); }
        catch (Exception e) { ItemHooks.Report($"{m.FullName}.{nameof(ModItem.UpdateArmorSet)}", e); }
    }

    static void PlayerFrame(On_Player.orig_PlayerFrame orig, Player self)
    {
        orig(self);
        try
        {
            Vanity(self, nameof(EquipTexture.PreUpdateVanitySet));
            PlayerLoader.Each(self, nameof(ModPlayer.FrameEffects), m => m.FrameEffects());
            foreach (EquipType type in EquipLoader.Installed())
            {
                if (EquipLoader.GetEquipTexture(type, EquipLoader.PlayerSlot(self, type)) is not EquipTexture texture) continue;
                try { texture.FrameEffects(self, type); }
                catch (Exception e) { ItemHooks.Report($"{texture.Name}.{nameof(EquipTexture.FrameEffects)}", e); }
            }
            Vanity(self, nameof(EquipTexture.UpdateVanitySet));
        }
        catch (Exception e) { ItemHooks.Report("vanity sets", e); }
    }

    static void SetArmorEffectVisuals(On_Player.orig_SetArmorEffectVisuals orig, Player self, Player drawPlayer)
    {
        orig(self, drawPlayer);
        try
        {
            Vanity(drawPlayer, nameof(EquipTexture.ArmorSetShadows));
            if (!drawPlayer.stoned && drawPlayer.stealth == 1f) return;
            self.armorEffectDrawOutlines = false;
            self.armorEffectDrawShadow = false;
            self.armorEffectDrawShadowSubtle = false;
        }
        catch (Exception e) { ItemHooks.Report("ArmorSetShadows", e); }
    }

    static void Vanity(Player player, string method)
    {
        int head = player.head, body = player.body, legs = player.legs;
        VanityPiece(player, method, EquipType.Head, head, head, body, legs);
        VanityPiece(player, method, EquipType.Body, body, head, body, legs);
        VanityPiece(player, method, EquipType.Legs, legs, head, body, legs);
        short[] overriding = ItemLoader.Globals.Overriding(method);
        if (overriding.Length == 0) return;
        GlobalItem[] templates = Globals;
        foreach (short index in overriding)
        {
            if (index >= templates.Length) continue;
            GlobalItem g = templates[index];
            string set;
            try { set = g.IsVanitySet(head, body, legs) ?? ""; }
            catch (Exception e)
            {
                ItemHooks.Report($"{g.FullName}.{nameof(GlobalItem.IsVanitySet)}", e);
                continue;
            }
            if (set.Length == 0) continue;
            try
            {
                switch (method)
                {
                    case nameof(GlobalItem.PreUpdateVanitySet): g.PreUpdateVanitySet(player, set); break;
                    case nameof(GlobalItem.UpdateVanitySet): g.UpdateVanitySet(player, set); break;
                    case nameof(GlobalItem.ArmorSetShadows): g.ArmorSetShadows(player, set); break;
                }
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.{method}", e); }
        }
    }

    static void VanityPiece(Player player, string method, EquipType type, int slot, int head, int body, int legs)
    {
        if (EquipLoader.GetEquipTexture(type, slot) is not EquipTexture texture) return;
        bool set;
        try { set = texture.IsVanitySet(head, body, legs); }
        catch (Exception e)
        {
            ItemHooks.Report($"{texture.Name}.{nameof(EquipTexture.IsVanitySet)}", e);
            return;
        }
        if (!set) return;
        try
        {
            switch (method)
            {
                case nameof(EquipTexture.PreUpdateVanitySet): texture.PreUpdateVanitySet(player); break;
                case nameof(EquipTexture.UpdateVanitySet): texture.UpdateVanitySet(player); break;
                case nameof(EquipTexture.ArmorSetShadows): texture.ArmorSetShadows(player); break;
            }
        }
        catch (Exception e) { ItemHooks.Report($"{texture.Name}.{method}", e); }
    }

    [UnmanagedCallersOnly]
    static int SetMatch(Player.SetMatchRequest* request, byte* somethingSpecial, IntPtr method)
    {
        Hook? hook = setMatch;
        if (hook == null) return -1;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg((IntPtr)request);
        call.Arg((IntPtr)somethingSpecial);
        call.Arg(method);
        int result = (int)ItemHooks.Original(ref call, hook, "Player.SetMatch");
        try
        {
            int part = request->ArmorSlotRequested;
            EquipType type = part == 1 ? EquipType.Body : part == 2 ? EquipType.Legs : EquipType.Head;
            int slot = part == 1 ? request->Body : part == 2 ? request->Legs : request->Head;
            EquipTexture? texture = EquipLoader.GetEquipTexture(type, slot);
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.SetMatch));
            if (texture == null && overriding.Length == 0) return result;
            bool male = request->Male;
            int equip = result;
            bool robes = *somethingSpecial != 0;
            if (texture != null)
            {
                try { texture.SetMatch(male, ref equip, ref robes); }
                catch (Exception e) { ItemHooks.Report($"{texture.Name}.SetMatch", e); }
            }
            GlobalItem[] templates = Globals;
            foreach (short index in overriding)
            {
                if (index >= templates.Length) continue;
                GlobalItem g = templates[index];
                try { g.SetMatch(part, slot, male, ref equip, ref robes); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.SetMatch", e); }
            }
            *somethingSpecial = robes ? (byte)1 : (byte)0;
            return equip;
        }
        catch (Exception e)
        {
            ItemHooks.Report("SetMatch", e);
            return result;
        }
    }

    static void Run(string who, string what, Action action)
    {
        try { action(); }
        catch (Exception e) { ItemHooks.Report($"{who}.{what}", e); }
    }
}
