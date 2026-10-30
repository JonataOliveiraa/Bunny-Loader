using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class PlayerJumpHooks
{
    sealed class JumpScope
    {
        public Player Player;
        public ExtraJumpLoader.PlayerJumps Jumps = null!;
        public bool[] Before = Array.Empty<bool>();
        public bool[] Announced = Array.Empty<bool>();
    }

    static JumpScope? jumping;

    static readonly string[] Hooks =
    {
        nameof(ModPlayer.ModifyExtraJumpDurationMultiplier), nameof(ModPlayer.CanStartExtraJump), nameof(ModPlayer.OnExtraJumpStarted),
        nameof(ModPlayer.OnExtraJumpEnded), nameof(ModPlayer.OnExtraJumpRefreshed), nameof(ModPlayer.ExtraJumpVisuals),
        nameof(ModPlayer.CanShowExtraJumpVisuals), nameof(ModPlayer.OnExtraJumpCleared),
    };

    internal static void Install(Func<string, bool> has)
    {
        foreach (string hook in Hooks)
        {
            if (!has(hook)) continue;
            InstallCore();
            break;
        }
        if (has(nameof(ModPlayer.OnExtraJumpStarted)))
            ItemHooks.Once("PlayerJumpSounds", () =>
            {
                On_SoundEngine.PlaySound_int_int_int_int_float_float += PlaySound;
                On_SoundEngine.PlaySound_LegacySoundStyle_Vector2_float_float += PlaySound;
            });
    }

    internal static void InstallCore() =>
        ItemHooks.Once("PlayerExtraJumps", () =>
        {
            On_Player.ResetEffects += ResetEffects;
            On_Player.UpdateJumpHeight += UpdateJumpHeight;
            On_Player.RefreshDoubleJumps += RefreshDoubleJumps;
            On_Player.JumpMovement += JumpMovement;
            On_Player.CancelAllJumpVisualEffects += CancelAllJumpVisualEffects;
            On_Player.DoubleJumpVisuals += DoubleJumpVisuals;
            PlayerUpdateHooks.WantRunSpeeds();
        });

    static int VanillaCount => ExtraJumpLoader.DefaultCount;

    static VanillaExtraJump? Native(int index) =>
        ExtraJumpLoader.ExtraJumps[index] is VanillaExtraJump { Native: true } jump ? jump : null;

    static void ResetEffects(On_Player.orig_ResetEffects orig, Player self)
    {
        orig(self);
        ExtraJumpLoader.ResetEnableFlags(self, ExtraJumpLoader.Of(self));
    }

    static void UpdateJumpHeight(On_Player.orig_UpdateJumpHeight orig, Player self)
    {
        ExtraJumpLoader.PlayerJumps jumps = ExtraJumpLoader.Of(self);
        ExtraJumpLoader.Sync(self, jumps);
        bool blocked = self.mount.Active && self.mount.BlockExtraJumps;
        bool grounded = self.velocity.Y == 0f || self.sliding;
        for (int i = 0; i < VanillaCount; i++)
        {
            if (Native(i) is not { } jump) continue;
            if (jumps.States[i]._disabled && jump.IsPerforming(self))
            {
                jump.IsPerforming(self) = false;
                self.jump = 0;
                ExtraJumpLoader.Ended(self, jump);
            }
            if (jump.CanJumpAgain(self) && (blocked || (!grounded && !jump.HasOption(self)))) ExtraJumpLoader.Cleared(self, jump);
        }
        if (blocked) ExtraJumpLoader.ConsumeModded(self, jumps);
        else if (!grounded) ExtraJumpLoader.ConsumeAndStopUnavailableModded(self, jumps);
        ExtraJumpLoader.PullAll(self, jumps);
        orig(self);
    }

    static void RefreshDoubleJumps(On_Player.orig_RefreshDoubleJumps orig, Player self)
    {
        orig(self);
        ExtraJumpLoader.PlayerJumps jumps = ExtraJumpLoader.Of(self);
        ExtraJumpLoader.PullAll(self, jumps);
        foreach (ExtraJump jump in ExtraJumpLoader.OrderedJumps)
        {
            if (Native(jump.Type) is { } vanilla && vanilla.HasOption(self))
                PlayerLoader.Each(self, nameof(ModPlayer.OnExtraJumpRefreshed), jump, static (m, j) => m.OnExtraJumpRefreshed(j));
        }
        ExtraJumpLoader.RefreshModded(self, jumps);
    }

    static bool Attempting(Player self)
    {
        if (!self.controlJump || self.jump != 0 || !self.releaseJump) return false;
        if (self.velocity.Y == 0f || self.sliding) return false;
        if (self.wet && self.accFlipper && (!self.mount.Active || !self.mount.Cart)) return false;
        if (self.mount.Active && self.mount.IsConsideredASlimeMount && self.wetSlime > 0) return false;
        if (self.hasDeadCellsDownDash && self.controlDown && !self.isPerformingJump_DownDash && !self.mount.Active) return false;
        return true;
    }

    static void JumpMovement(On_Player.orig_JumpMovement orig, Player self)
    {
        ExtraJumpLoader.PlayerJumps jumps = ExtraJumpLoader.Of(self);
        ExtraJumpLoader.Sync(self, jumps);
        var scope = new JumpScope { Player = self, Jumps = jumps, Before = new bool[VanillaCount], Announced = new bool[VanillaCount] };
        for (int i = 0; i < VanillaCount; i++) scope.Before[i] = Native(i)?.IsPerforming(self) ?? false;
        Span<bool> hidden = stackalloc bool[VanillaCount];
        if (Attempting(self))
        {
            ExtraJump? chosen = null;
            if (!jumps.Block)
            {
                foreach (ExtraJump jump in ExtraJumpLoader.OrderedJumps)
                {
                    if (jump == ExtraJump.Flipper || !jumps.States[jump.Type].Available || !ExtraJumpLoader.CanStart(self, jump)) continue;
                    chosen = jump;
                    break;
                }
            }
            for (int i = 0; i < VanillaCount; i++)
            {
                if (Native(i) is not { } jump || ExtraJumpLoader.ExtraJumps[i] == chosen || !jump.CanJumpAgain(self)) continue;
                jump.CanJumpAgain(self) = false;
                hidden[i] = true;
            }
            if (chosen != null && chosen.Type >= VanillaCount)
            {
                ExtraJumpLoader.PerformJump(chosen, self, jumps);
                self.releaseJump = false;
            }
        }
        JumpScope? outer = jumping;
        jumping = scope;
        try { orig(self); }
        finally
        {
            jumping = outer;
            for (int i = 0; i < VanillaCount; i++)
            {
                if (hidden[i]) Native(i)!.CanJumpAgain(self) = true;
            }
        }
        for (int i = 0; i < VanillaCount; i++)
        {
            if (Native(i) is not { } jump) continue;
            bool now = jump.IsPerforming(self);
            if (now && !scope.Before[i])
            {
                float duration = ExtraJumpLoader.Duration(self, jump);
                if (duration != jump.GetDurationMultiplier(self)) self.jump = Math.Max(0, (int)(Player.jumpHeight * duration));
                Announce(scope, i);
            }
            else if (!now && scope.Before[i]) ExtraJumpLoader.Ended(self, jump);
        }
        ExtraJumpLoader.PullAll(self, jumps);
    }

    static bool Announce(JumpScope scope, int index)
    {
        if (scope.Announced[index]) return true;
        scope.Announced[index] = true;
        return ExtraJumpLoader.Started(scope.Player, ExtraJumpLoader.ExtraJumps[index], true);
    }

    static bool SoundAllowed()
    {
        JumpScope? scope = jumping;
        if (scope == null) return true;
        bool sound = true;
        for (int i = 0; i < VanillaCount; i++)
        {
            if (Native(i) is not { } jump || scope.Before[i] || scope.Announced[i] || !jump.IsPerforming(scope.Player)) continue;
            if (!Announce(scope, i)) sound = false;
        }
        return sound;
    }

    static SoundEffectInstance PlaySound(On_SoundEngine.orig_PlaySound_int_int_int_int_float_float orig, int type, int x, int y, int Style, float volumeScale, float pitchOffset) =>
        SoundAllowed() ? orig(type, x, y, Style, volumeScale, pitchOffset) : default;

    static SoundEffectInstance PlaySound(On_SoundEngine.orig_PlaySound_LegacySoundStyle_Vector2_float_float orig, LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale) =>
        SoundAllowed() ? orig(type, position, pitchOffset, volumeScale) : default;

    static void CancelAllJumpVisualEffects(On_Player.orig_CancelAllJumpVisualEffects orig, Player self, bool includeDownDash)
    {
        Span<bool> before = stackalloc bool[VanillaCount];
        for (int i = 0; i < VanillaCount; i++) before[i] = Native(i)?.IsPerforming(self) ?? false;
        orig(self, includeDownDash);
        ExtraJumpLoader.PlayerJumps jumps = ExtraJumpLoader.Of(self);
        for (int i = 0; i < VanillaCount; i++)
        {
            if (before[i] && Native(i) is { } jump && !jump.IsPerforming(self)) ExtraJumpLoader.Ended(self, jump);
        }
        ExtraJumpLoader.StopModdedJumps(self, jumps);
        ExtraJumpLoader.PullAll(self, jumps);
    }

    static void DoubleJumpVisuals(On_Player.orig_DoubleJumpVisuals orig, Player self)
    {
        ExtraJumpLoader.PlayerJumps jumps = ExtraJumpLoader.Of(self);
        Span<bool> hidden = stackalloc bool[VanillaCount];
        for (int i = 0; i < VanillaCount; i++)
        {
            if (Native(i) is not { } jump || !jump.IsPerforming(self)) continue;
            if (!PlayerLoader.All(self, nameof(ModPlayer.CanShowExtraJumpVisuals), (ExtraJump)jump, static (m, j) => m.CanShowExtraJumpVisuals(j)))
            {
                hidden[i] = true;
                jump.IsPerforming(self) = false;
            }
            else PlayerLoader.Each(self, nameof(ModPlayer.ExtraJumpVisuals), (ExtraJump)jump, static (m, j) => m.ExtraJumpVisuals(j));
        }
        try { orig(self); }
        finally
        {
            for (int i = 0; i < VanillaCount; i++)
            {
                if (hidden[i]) Native(i)!.IsPerforming(self) = true;
            }
        }
        ExtraJumpLoader.ModdedVisuals(self, jumps);
    }
}
