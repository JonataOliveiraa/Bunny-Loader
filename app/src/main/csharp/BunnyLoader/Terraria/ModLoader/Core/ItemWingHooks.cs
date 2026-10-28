using System;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemWingHooks
{
    internal const string Vertical = "Vertical", Horizontal = "Horizontal", Update = "Update";

    static bool global;

    internal static void Install(Func<string, bool> has, bool isGlobal)
    {
        if (has(nameof(ModItem.VerticalWingSpeeds))) Want(Vertical, isGlobal);
        if (has(nameof(ModItem.HorizontalWingSpeeds))) Want(Horizontal, isGlobal);
        if (has(nameof(ModItem.WingUpdate))) Want(Update, isGlobal);
    }

    internal static void Want(string method, bool isGlobal)
    {
        if (isGlobal) global = true;
        switch (method)
        {
            case Vertical: ItemHooks.Once("WingVertical", () => On_Player.WingMovement += WingMovement); break;
            case Horizontal: ItemHooks.Once("WingHorizontal", () => On_Player.WingAirLogicTweaks += WingAirLogicTweaks); break;
            case Update: ItemHooks.Once("WingUpdate", () => On_Player.WingFrame += WingFrame); break;
        }
    }

    static bool Ours(int slot) => global || slot >= EquipLoader.VanillaCount(EquipType.Wings) && EquipLoader.VanillaCount(EquipType.Wings) > 0;

    static Item Equipped(Player player, int slot)
    {
        var armor = player.armor;
        for (int i = 3; i < 10 && i < armor.Length; i++)
        {
            Item item = armor[i];
            if (item.wingSlot == slot && player.IsItemSlotUnlockedAndUsable(i)) return item;
        }
        return default;
    }

    static void WingMovement(On_Player.orig_WingMovement orig, Player self)
    {
        int logic = self.wingsLogic;
        if (!Ours(logic) || logic == 4 && self.TryingToHoverUp)
        {
            orig(self);
            return;
        }
        try { Movement(self, logic); }
        catch (Exception e)
        {
            ItemHooks.Report("VerticalWingSpeeds", e);
            orig(self);
        }
    }

    static void Movement(Player player, int logic)
    {
        float jump = Player.jumpSpeed;
        float constant = 0.1f, falling = 0.5f, maxAscent = 1.5f, maxCan = 0.5f, rising = 0.1f;
        if (logic == 26 || logic == 37) (falling, rising, maxCan, maxAscent, constant) = (0.75f, 0.15f, 1f, 2.5f, 0.125f);
        if (logic is 8 or 11 or 24 or 27 or 22) maxAscent = 1.66f;
        if (logic is 21 or 12 or 20 or 23) maxAscent = 1.805f;
        if (logic == 44)
        {
            (falling, rising, maxCan, maxAscent, constant) = (0.85f, 0.15f, 1f, 2.75f, 0.125f);
            Hover(player, jump);
        }
        if (logic == 45)
        {
            (falling, rising, maxCan, maxAscent) = (0.95f, 0.15f, 1f, 4.5f);
            Hover(player, jump);
        }
        if (logic == 29 || logic == 32) (falling, rising, maxCan, maxAscent, constant) = (0.85f, 0.15f, 1f, 3f, 0.135f);
        if (logic == 30 || logic == 31)
        {
            (maxCan, maxAscent) = (1f, 2.45f);
            if (!player.TryingToHoverDown) constant = 0.15f;
        }

        Item item = Equipped(player, logic);
        if (!item.IsNull && ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.VerticalWingSpeeds(player, ref falling, ref rising, ref maxCan, ref maxAscent, ref constant); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.VerticalWingSpeeds", e); }
        }
        else if (item.IsNull && EquipLoader.GetEquipTexture(EquipType.Wings, logic) is EquipTexture texture)
        {
            try { texture.VerticalWingSpeeds(player, ref falling, ref rising, ref maxCan, ref maxAscent, ref constant); }
            catch (Exception e) { ItemHooks.Report($"{texture.Name}.VerticalWingSpeeds", e); }
        }
        foreach (GlobalItem g in ItemLoader.Globals.Templates)
        {
            if (!ItemHooks.Wants(g, nameof(GlobalItem.VerticalWingSpeeds))) continue;
            try { g.VerticalWingSpeeds(item, player, ref falling, ref rising, ref maxCan, ref maxAscent, ref constant); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.VerticalWingSpeeds", e); }
        }

        float grav = player.gravDir;
        Vector2 velocity = player.velocity;
        float y = velocity.Y - constant * grav;
        if (grav == 1f)
        {
            if (y > 0f) y -= falling;
            else if (y > -jump * maxCan) y -= rising;
            if (y < -jump * maxAscent) y = -jump * maxAscent;
        }
        else
        {
            if (y < 0f) y += falling;
            else if (y < jump * maxCan) y += rising;
            if (y > jump * maxAscent) y = jump * maxAscent;
        }
        player.velocity.Y = y;

        bool slowDown = logic is 22 or 28 or 30 or 31 or 37 or 45 && player.TryingToHoverDown && !player.controlLeft && !player.controlRight;
        player.wingTime -= slowDown ? 0.5f : 1f;
        if (player.empressBrooch && player.wingTime != 0f) player.wingTime = player.wingTimeMax;
    }

    static void Hover(Player player, float jump)
    {
        if (player.TryingToHoverUp)
        {
            float grav = player.gravDir;
            float y = player.velocity.Y - 0.4f * grav;
            if (grav == 1f)
            {
                if (y > 0f) y -= 1f;
                else if (y > -jump) y -= 0.2f;
                if (y < -jump * 3f) y = -jump * 3f;
            }
            else
            {
                if (y < 0f) y += 1f;
                else if (y < jump) y += 0.2f;
                if (y > jump * 3f) y = jump * 3f;
            }
            player.velocity.Y = y;
        }
        if (player.TryingToHoverDown && !player.controlJump && player.velocity.Y != 0f) player.velocity.Y += 0.4f;
    }

    static void WingAirLogicTweaks(On_Player.orig_WingAirLogicTweaks orig, Player self)
    {
        orig(self);
        int logic = self.wingsLogic;
        if (self.wings <= 0 || !Ours(logic)) return;
        try
        {
            float speed = self.accRunSpeed, acceleration = self.runAcceleration;
            Item item = Equipped(self, logic);
            if (!item.IsNull && ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.HorizontalWingSpeeds(self, ref speed, ref acceleration); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.HorizontalWingSpeeds", e); }
            }
            else if (item.IsNull && EquipLoader.GetEquipTexture(EquipType.Wings, logic) is EquipTexture texture)
            {
                try { texture.HorizontalWingSpeeds(self, ref speed, ref acceleration); }
                catch (Exception e) { ItemHooks.Report($"{texture.Name}.HorizontalWingSpeeds", e); }
            }
            foreach (GlobalItem g in ItemLoader.Globals.Templates)
            {
                if (!ItemHooks.Wants(g, nameof(GlobalItem.HorizontalWingSpeeds))) continue;
                try { g.HorizontalWingSpeeds(item, self, ref speed, ref acceleration); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.HorizontalWingSpeeds", e); }
            }
            self.accRunSpeed = speed;
            self.runAcceleration = acceleration;
        }
        catch (Exception e) { ItemHooks.Report("HorizontalWingSpeeds", e); }
    }

    static void WingFrame(On_Player.orig_WingFrame orig, Player self, bool wingFlap)
    {
        int wings = self.wings;
        if (!Ours(wings))
        {
            orig(self, wingFlap);
            return;
        }
        bool custom = false;
        try
        {
            if (EquipLoader.GetEquipTexture(EquipType.Wings, wings) is EquipTexture texture)
            {
                try { custom = texture.WingUpdate(self, wingFlap); }
                catch (Exception e) { ItemHooks.Report($"{texture.Name}.WingUpdate", e); }
            }
            foreach (GlobalItem g in ItemLoader.Globals.Templates)
            {
                if (!ItemHooks.Wants(g, nameof(GlobalItem.WingUpdate))) continue;
                try { custom |= g.WingUpdate(wings, self, wingFlap); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.WingUpdate", e); }
            }
        }
        catch (Exception e) { ItemHooks.Report("WingUpdate", e); }
        if (!custom) orig(self, wingFlap);
    }
}
