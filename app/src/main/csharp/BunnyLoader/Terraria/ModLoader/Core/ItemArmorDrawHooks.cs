using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemArmorDrawHooks
{
    static volatile bool hair, skin, colors;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.DrawArmorColor)) || has(nameof(ModItem.ArmorArmGlowMask))) WantColors();
    }

    internal static void WantColors()
    {
        colors = true;
        WantSetup();
    }

    internal static void WantHair()
    {
        if (hair) return;
        hair = true;
        ItemHooks.Once("ArmorHair", () => On_Player.GetHairSettings += GetHairSettings);
    }

    internal static void WantSkin()
    {
        if (skin) return;
        skin = true;
        WantSetup();
    }

    static void WantSetup() => ItemHooks.Once("ArmorDrawSetup", () => On_PlayerDrawSet.BoringSetup += BoringSetup);

    static void GetHairSettings(On_Player.orig_GetHairSettings orig, Player self, out bool fullHair, out bool hatHair, out bool hideHair,
                                out bool backHairDraw, out bool drawsBackHairWithoutHeadgear)
    {
        orig(self, out fullHair, out hatHair, out hideHair, out backHairDraw, out drawsBackHairWithoutHeadgear);
        try
        {
            int head = self.head;
            if (head < 0) return;
            fullHair |= ArmorIDs.Head.Sets.FullHair[head];
            hatHair |= ArmorIDs.Head.Sets.HatHair[head];
            drawsBackHairWithoutHeadgear |= ArmorIDs.Head.Sets.BackHairWithoutHeadgear[head];
        }
        catch (Exception e) { ItemHooks.Report("GetHairSettings", e); }
    }

    static void BoringSetup(On_PlayerDrawSet.orig_BoringSetup orig, PlayerDrawSet self, Player player, ref Vector2 drawPosition, float shadowOpacity,
                            float rotation, ref Vector2 rotationOrigin, Projectile overrideHeldProjectile)
    {
        orig(self, player, ref drawPosition, shadowOpacity, rotation, ref rotationOrigin, overrideHeldProjectile);
        try
        {
            if (skin) Skin(self, player);
            if (colors) Colors(self, player);
        }
        catch (Exception e) { ItemHooks.Report("PlayerDrawSet.BoringSetup", e); }
    }

    static void Skin(PlayerDrawSet set, Player player)
    {
        int body = player.body, legs = player.legs;
        if (body > 0 && ArmorIDs.Body.Sets.TopSkin[body] || legs > 0 && ArmorIDs.Legs.Sets.TopSkin[legs]) set.hidesTopSkin = true;
        if (body > 0 && ArmorIDs.Body.Sets.BottomSkin[body] || legs > 0 && ArmorIDs.Legs.Sets.BottomSkin[legs]) set.hidesBottomSkin = true;
        if (body > 0 && !ArmorIDs.Body.Sets.Hands[body]) set.missingHand = true;
        if (body > 0 && ArmorIDs.Body.Sets.Arms[body]) set.missingArm = false;
    }

    static void Colors(PlayerDrawSet set, Player player)
    {
        float shadow = set.shadow;
        Armor(EquipType.Head, player.head, player, shadow, ref set.colorArmorHead, ref set.headGlowMask, ref set.headGlowColor);
        Armor(EquipType.Body, player.body, player, shadow, ref set.colorArmorBody, ref set.bodyGlowMask, ref set.bodyGlowColor);
        Arm(player.body, player, shadow, ref set.armGlowMask, ref set.armGlowColor);
        Armor(EquipType.Legs, player.legs, player, shadow, ref set.colorArmorLegs, ref set.legsGlowMask, ref set.legsGlowColor);
    }

    static void Armor(EquipType type, int slot, Player player, float shadow, ref Color color, ref int glowMask, ref Color glowColor)
    {
        EquipTexture? texture = EquipLoader.GetEquipTexture(type, slot);
        GlobalItem[] globals = ItemLoader.Globals.Templates;
        if (texture == null && !Array.Exists(globals, g => ItemHooks.Wants(g, nameof(GlobalItem.DrawArmorColor)))) return;
        uint glowBefore = Packed(glowColor);
        Color tint = color;
        int glow = glowMask;
        Color glowTint = glowColor;
        if (texture != null)
        {
            try { texture.DrawArmorColor(player, shadow, ref tint, ref glow, ref glowTint); }
            catch (Exception e) { ItemHooks.Report($"{texture.Name}.DrawArmorColor", e); }
        }
        foreach (GlobalItem g in globals)
        {
            if (!ItemHooks.Wants(g, nameof(GlobalItem.DrawArmorColor))) continue;
            try { g.DrawArmorColor(type, slot, player, shadow, ref tint, ref glow, ref glowTint); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.DrawArmorColor", e); }
        }
        if (Packed(color) >> 24 != 0) color = tint;
        glowMask = glow;
        if (Packed(glowTint) != glowBefore) glowColor = player.GetImmuneAlphaPure(glowTint, shadow);
    }

    static void Arm(int slot, Player player, float shadow, ref int glowMask, ref Color glowColor)
    {
        EquipTexture? texture = EquipLoader.GetEquipTexture(EquipType.Body, slot);
        GlobalItem[] globals = ItemLoader.Globals.Templates;
        if (texture == null && !Array.Exists(globals, g => ItemHooks.Wants(g, nameof(GlobalItem.ArmorArmGlowMask)))) return;
        uint before = Packed(glowColor);
        int glow = glowMask;
        Color tint = glowColor;
        if (texture != null)
        {
            try { texture.ArmorArmGlowMask(player, shadow, ref glow, ref tint); }
            catch (Exception e) { ItemHooks.Report($"{texture.Name}.ArmorArmGlowMask", e); }
        }
        foreach (GlobalItem g in globals)
        {
            if (!ItemHooks.Wants(g, nameof(GlobalItem.ArmorArmGlowMask))) continue;
            try { g.ArmorArmGlowMask(slot, player, shadow, ref glow, ref tint); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ArmorArmGlowMask", e); }
        }
        glowMask = glow;
        if (Packed(tint) != before) glowColor = player.GetImmuneAlphaPure(tint, shadow);
    }

    static uint Packed(Color color) => Unsafe.As<Color, uint>(ref color);
}
