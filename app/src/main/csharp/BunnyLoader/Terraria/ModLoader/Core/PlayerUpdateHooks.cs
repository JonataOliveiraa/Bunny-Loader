using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Terraria.GameInput;

namespace Terraria.ModLoader.Core;

internal static unsafe class PlayerUpdateHooks
{
    static readonly HashSet<IntPtr> Moved = new();

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.PostUpdateMiscEffects))) ItemHooks.Once("PlayerMiscEffects", () => On_Player.CapAttackSpeeds += CapAttackSpeeds);
        if (has(nameof(ModPlayer.PostUpdateRunSpeeds))) ItemHooks.Once("PlayerRunSpeeds", () => On_Player.HorizontalMovement += HorizontalMovement);
        if (has(nameof(ModPlayer.PreUpdateMovement)))
            ItemHooks.Once("PlayerPreMovement", () =>
            {
                On_Player.Update += Update;
                On_Player.WetCollision += WetCollision;
                On_Player.DryCollision += DryCollision;
                On_Player.SlopingCollision += SlopingCollision;
                On_Player.BordersMovement += BordersMovement;
            });
        if (has(nameof(ModPlayer.NaturalLifeRegen)))
            ItemHooks.Once("PlayerNaturalRegen", () =>
            {
                if (Host.Native()->OnNaturalRegen(&NaturalRegen) == 0) Log.Error("player hooks: NaturalLifeRegen is not hooked");
            });
        if (has(nameof(ModPlayer.UpdateAutopause))) ItemHooks.Once("PlayerAutopause", () => On_Main.DoUpdate_WhilePaused += WhilePaused);
        if (has(nameof(ModPlayer.ProcessTriggers))) ItemHooks.Once("PlayerTriggers", () => On_TriggersSet.CopyInto += CopyInto);
        if (has(nameof(ModPlayer.ResetInfoAccessories)))
            ItemHooks.Once("PlayerInfoAccessories", () => On_Player.RefreshInfoAccs += RefreshInfoAccs);
        if (has(nameof(ModPlayer.ArmorSetBonusActivated))) ItemHooks.Once("PlayerSetBonusActivated", () => On_Player.KeyDoubleTap += KeyDoubleTap);
        if (has(nameof(ModPlayer.ArmorSetBonusHeld))) ItemHooks.Once("PlayerSetBonusHeld", () => On_Player.KeyHoldDown += KeyHoldDown);
        if (has(nameof(ModPlayer.OnEquipmentLoadoutSwitched))) ItemHooks.Once("PlayerLoadout", () => On_Player.TrySwitchingLoadout += TrySwitchingLoadout);
        if (has(nameof(ModPlayer.UpdateDyes))) ItemHooks.Once("PlayerDyes", () => On_Player.UpdateDyes += UpdateDyes);
        if (has(nameof(ModPlayer.UpdateVisibleAccessories)) || has(nameof(ModPlayer.UpdateVisibleVanityAccessories)))
            ItemHooks.Once("PlayerVisibleAccessories", () => On_Player.UpdateVisibleAccessories += UpdateVisibleAccessories);
    }

    static void CapAttackSpeeds(On_Player.orig_CapAttackSpeeds orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.PostUpdateMiscEffects), m => m.PostUpdateMiscEffects());
    }

    static void HorizontalMovement(On_Player.orig_HorizontalMovement orig, Player self)
    {
        PlayerLoader.Each(self, nameof(ModPlayer.PostUpdateRunSpeeds), m => m.PostUpdateRunSpeeds());
        orig(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        Moved.Remove(self.Pointer);
        try { orig(self, i); }
        finally { Moved.Remove(self.Pointer); }
    }

    static void BeforeMovement(Player self)
    {
        if (self.dead || !Moved.Add(self.Pointer)) return;
        PlayerLoader.Each(self, nameof(ModPlayer.PreUpdateMovement), m => m.PreUpdateMovement());
    }

    static void WetCollision(On_Player.orig_WetCollision orig, Player self, bool fallThrough, bool ignorePlats, float movementSpeed)
    {
        BeforeMovement(self);
        orig(self, fallThrough, ignorePlats, movementSpeed);
    }

    static void DryCollision(On_Player.orig_DryCollision orig, Player self, bool fallThrough, bool ignorePlats)
    {
        BeforeMovement(self);
        orig(self, fallThrough, ignorePlats);
    }

    static void SlopingCollision(On_Player.orig_SlopingCollision orig, Player self, bool fallThrough, bool ignorePlats)
    {
        BeforeMovement(self);
        orig(self, fallThrough, ignorePlats);
    }

    static void BordersMovement(On_Player.orig_BordersMovement orig, Player self)
    {
        BeforeMovement(self);
        orig(self);
    }

    [UnmanagedCallersOnly]
    static float NaturalRegen(IntPtr player, float regen)
    {
        try
        {
            float value = regen;
            PlayerLoader.Each(new Player(player), nameof(ModPlayer.NaturalLifeRegen), 0, ref value, static (m, _, ref r) => m.NaturalLifeRegen(ref r));
            return float.IsFinite(value) ? value : regen;
        }
        catch (Exception e)
        {
            ItemHooks.Report("NaturalLifeRegen", e);
            return regen;
        }
    }

    static void WhilePaused(On_Main.orig_DoUpdate_WhilePaused orig)
    {
        orig();
        if (Main.gameMenu) return;
        Player player = Main.LocalPlayer;
        if (!player.IsNull) PlayerLoader.Each(player, nameof(ModPlayer.UpdateAutopause), m => m.UpdateAutopause());
    }

    static void CopyInto(On_TriggersSet.orig_CopyInto orig, TriggersSet self, Player p)
    {
        orig(self, p);
        if (p.whoAmI == Main.myPlayer) PlayerLoader.Each(p, nameof(ModPlayer.ProcessTriggers), self, static (m, t) => m.ProcessTriggers(t));
    }

    static void RefreshInfoAccs(On_Player.orig_RefreshInfoAccs orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.ResetInfoAccessories), m => m.ResetInfoAccessories());
    }

    static int SetBonusKey => Main.ReversedUpDownArmorSetBonuses ? 1 : 0;

    static void KeyDoubleTap(On_Player.orig_KeyDoubleTap orig, Player self, int keyDir)
    {
        orig(self, keyDir);
        if (keyDir == SetBonusKey) PlayerLoader.Each(self, nameof(ModPlayer.ArmorSetBonusActivated), m => m.ArmorSetBonusActivated());
    }

    static void KeyHoldDown(On_Player.orig_KeyHoldDown orig, Player self, int keyDir, int holdTime)
    {
        orig(self, keyDir, holdTime);
        if (keyDir == SetBonusKey) PlayerLoader.Each(self, nameof(ModPlayer.ArmorSetBonusHeld), holdTime, static (m, t) => m.ArmorSetBonusHeld(t));
    }

    static void TrySwitchingLoadout(On_Player.orig_TrySwitchingLoadout orig, Player self, int loadoutIndex)
    {
        int previous = self.CurrentLoadoutIndex;
        orig(self, loadoutIndex);
        int current = self.CurrentLoadoutIndex;
        if (previous != current)
            PlayerLoader.Each(self, nameof(ModPlayer.OnEquipmentLoadoutSwitched), (previous, current), static (m, s) => m.OnEquipmentLoadoutSwitched(s.previous, s.current));
    }

    static void UpdateDyes(On_Player.orig_UpdateDyes orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateDyes), m => m.UpdateDyes());
    }

    static void UpdateVisibleAccessories(On_Player.orig_UpdateVisibleAccessories orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateVisibleAccessories), m => m.UpdateVisibleAccessories());
        PlayerLoader.Each(self, nameof(ModPlayer.UpdateVisibleVanityAccessories), m => m.UpdateVisibleVanityAccessories());
    }
}
