using System;
using System.Runtime.InteropServices;
using BunnyLoader;
using Terraria.GameInput;

namespace Terraria.ModLoader.Core;

internal static unsafe class PlayerUpdateHooks
{
    static readonly PlayerHook HookPostUpdateMiscEffects = PlayerLoader.Hook(nameof(ModPlayer.PostUpdateMiscEffects));
    static readonly PlayerHook HookPostUpdateRunSpeeds = PlayerLoader.Hook(nameof(ModPlayer.PostUpdateRunSpeeds));
    static readonly PlayerHook HookPreUpdateMovement = PlayerLoader.Hook(nameof(ModPlayer.PreUpdateMovement));
    static readonly PlayerHook HookNaturalLifeRegen = PlayerLoader.Hook(nameof(ModPlayer.NaturalLifeRegen));
    static readonly PlayerHook HookProcessTriggers = PlayerLoader.Hook(nameof(ModPlayer.ProcessTriggers));
    static readonly PlayerHook HookSetControls = PlayerLoader.Hook(nameof(ModPlayer.SetControls));
    static readonly PlayerHook HookResetInfoAccessories = PlayerLoader.Hook(nameof(ModPlayer.ResetInfoAccessories));
    static readonly PlayerHook HookArmorSetBonusActivated = PlayerLoader.Hook(nameof(ModPlayer.ArmorSetBonusActivated));
    static readonly PlayerHook HookArmorSetBonusHeld = PlayerLoader.Hook(nameof(ModPlayer.ArmorSetBonusHeld));
    static readonly PlayerHook HookUpdateDyes = PlayerLoader.Hook(nameof(ModPlayer.UpdateDyes));
    static readonly PlayerHook HookUpdateVisibleAccessories = PlayerLoader.Hook(nameof(ModPlayer.UpdateVisibleAccessories));
    static readonly PlayerHook HookUpdateVisibleVanityAccessories = PlayerLoader.Hook(nameof(ModPlayer.UpdateVisibleVanityAccessories));

    internal static void Install(Func<string, bool> has)
    {
        PlayerExampleHooks.Install(has);
        if (has(nameof(ModPlayer.PostUpdateMiscEffects))) ItemHooks.Once("PlayerMiscEffects", () => On_Player.CapAttackSpeeds += CapAttackSpeeds);
        if (has(nameof(ModPlayer.PostUpdateRunSpeeds))) WantRunSpeeds();
        if (has(nameof(ModPlayer.PreUpdateMovement))) ItemHooks.Once("PlayerPreMovement", () => On_Player.UpdateNetOffset += UpdateNetOffset);
        if (has(nameof(ModPlayer.NaturalLifeRegen)))
            ItemHooks.Once("PlayerNaturalRegen", () =>
            {
                if (Host.Native()->OnNaturalRegen(&NaturalRegen) == 0) Log.Error("player hooks: NaturalLifeRegen is not hooked");
            });
        if (has(nameof(ModPlayer.UpdateAutopause))) ItemHooks.Once("PlayerAutopause", () => On_Main.DoUpdate_WhilePaused += WhilePaused);
        if (has(nameof(ModPlayer.ProcessTriggers)) || has(nameof(ModPlayer.SetControls))) ItemHooks.Once("PlayerTriggers", () => On_TriggersSet.CopyInto += CopyInto);
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
        PlayerLoader.Each(self, HookPostUpdateMiscEffects, static m => m.PostUpdateMiscEffects());
    }

    internal static void WantRunSpeeds() => ItemHooks.Once("PlayerRunSpeeds", () => On_Player.HorizontalMovement += HorizontalMovement);

    static void HorizontalMovement(On_Player.orig_HorizontalMovement orig, Player self)
    {
        ExtraJumpLoader.UpdateHorizontalSpeeds(self);
        PlayerLoader.Each(self, HookPostUpdateRunSpeeds, static m => m.PostUpdateRunSpeeds());
        orig(self);
    }

    static void UpdateNetOffset(On_Player.orig_UpdateNetOffset orig, Player self, bool fallThrough, bool ignorePlats)
    {
        if (!self.ghost) PlayerLoader.Each(self, HookPreUpdateMovement, static m => m.PreUpdateMovement());
        orig(self, fallThrough, ignorePlats);
    }

    [UnmanagedCallersOnly]
    static float NaturalRegen(IntPtr player, float regen)
    {
        try
        {
            float value = regen;
            PlayerLoader.Each(new Player(player), HookNaturalLifeRegen, 0, ref value, static (m, _, ref r) => m.NaturalLifeRegen(ref r));
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
        if (p.whoAmI != Main.myPlayer) return;
        PlayerLoader.Each(p, HookProcessTriggers, self, static (m, t) => m.ProcessTriggers(t));
        PlayerLoader.Each(p, HookSetControls, static m => m.SetControls());
    }

    static void RefreshInfoAccs(On_Player.orig_RefreshInfoAccs orig, Player self)
    {
        PlayerLoader.Each(self, HookResetInfoAccessories, static m => m.ResetInfoAccessories());
        orig(self);
    }

    static int SetBonusKey => Main.ReversedUpDownArmorSetBonuses ? 1 : 0;

    static void KeyDoubleTap(On_Player.orig_KeyDoubleTap orig, Player self, int keyDir)
    {
        orig(self, keyDir);
        if (keyDir == SetBonusKey) PlayerLoader.Each(self, HookArmorSetBonusActivated, static m => m.ArmorSetBonusActivated());
    }

    static void KeyHoldDown(On_Player.orig_KeyHoldDown orig, Player self, int keyDir, int holdTime)
    {
        orig(self, keyDir, holdTime);
        if (keyDir == SetBonusKey) PlayerLoader.Each(self, HookArmorSetBonusHeld, holdTime, static (m, t) => m.ArmorSetBonusHeld(t));
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
        PlayerLoader.Each(self, HookUpdateDyes, static m => m.UpdateDyes());
    }

    static void UpdateVisibleAccessories(On_Player.orig_UpdateVisibleAccessories orig, Player self)
    {
        orig(self);
        PlayerLoader.Each(self, HookUpdateVisibleAccessories, static m => m.UpdateVisibleAccessories());
        PlayerLoader.Each(self, HookUpdateVisibleVanityAccessories, static m => m.UpdateVisibleVanityAccessories());
    }
}
