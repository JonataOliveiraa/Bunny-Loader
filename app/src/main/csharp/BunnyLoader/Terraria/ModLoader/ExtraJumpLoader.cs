using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class ExtraJumpLoader
{
    internal sealed class PlayerJumps
    {
        public ExtraJumpState[] States = Array.Empty<ExtraJumpState>();
        public bool[] SnapEnabled = Array.Empty<bool>(), SnapAvailable = Array.Empty<bool>(), SnapActive = Array.Empty<bool>();
        public bool Block;
    }

    public static int ExtraJumpCount => ExtraJumps.Count;

    internal static readonly List<ExtraJump> ExtraJumps = new()
    {
        ExtraJump.Flipper,
        ExtraJump.BasiliskMount,
        ExtraJump.GoatMount,
        ExtraJump.SantankMount,
        ExtraJump.UnicornMount,
        ExtraJump.SandstormInABottle,
        ExtraJump.BlizzardInABottle,
        ExtraJump.FartInAJar,
        ExtraJump.TsunamiInABottle,
        ExtraJump.CloudInABottle,
    };

    static readonly int DefaultExtraJumpCount = ExtraJumps.Count;
    static readonly EntityInstances<PlayerJumps> Players = new();
    static ExtraJump[]? orderedJumps;

    static ExtraJumpLoader()
    {
        for (int i = 0; i < ExtraJumps.Count; i++) ExtraJumps[i].Type = i;
    }

    public static IReadOnlyList<ExtraJump> OrderedJumps => orderedJumps ??= Order();

    internal static bool HasModded => ExtraJumps.Count > DefaultExtraJumpCount;

    static IEnumerable<ExtraJump> ModdedExtraJumps => ExtraJumps.Skip(DefaultExtraJumpCount);

    internal static int Add(ExtraJump jump)
    {
        lock (ExtraJumps)
        {
            ExtraJumps.Add(jump);
            orderedJumps = null;
        }
        PlayerJumpHooks.InstallCore();
        return ExtraJumps.Count - 1;
    }

    public static ExtraJump Get(int index) => index < 0 || index >= ExtraJumpCount ? null! : ExtraJumps[index];

    internal static void ResizeArrays() => orderedJumps = Order();

    static ExtraJump[] Order()
    {
        if (!HasModded) return ExtraJumps.ToArray();
        var slots = new List<ExtraJump>[DefaultExtraJumpCount + 1];
        for (int i = 0; i < slots.Length; i++) slots[i] = new List<ExtraJump>();
        foreach (ExtraJump jump in ModdedExtraJumps)
        {
            switch (jump.GetDefaultPosition())
            {
                case ExtraJump.After after:
                    if (after.Target is not null and not VanillaExtraJump)
                        throw new ArgumentException($"ExtraJump {jump} did not refer to a vanilla ExtraJump in GetDefaultPosition()");
                    slots[after.Target is null ? 0 : after.Target.Type + 1].Add(jump);
                    break;
                case ExtraJump.Before before:
                    if (before.Target is not null and not VanillaExtraJump)
                        throw new ArgumentException($"ExtraJump {jump} did not refer to a vanilla ExtraJump in GetDefaultPosition()");
                    slots[before.Target is null ? slots.Length - 1 : before.Target.Type].Add(jump);
                    break;
                default:
                    throw new ArgumentException($"ExtraJump {jump} has unknown Position {jump.GetDefaultPosition()}");
            }
        }
        var sorted = new List<ExtraJump>();
        for (int i = 0; i < slots.Length; i++)
        {
            sorted.AddRange(Sort(slots[i]));
            if (i < DefaultExtraJumpCount) sorted.Add(ExtraJumps[i]);
        }
        return sorted.ToArray();
    }

    static List<ExtraJump> Sort(List<ExtraJump> elements)
    {
        var after = elements.ToDictionary(e => e, _ => new HashSet<ExtraJump>());
        foreach (ExtraJump jump in elements)
        {
            foreach (ExtraJump.Position position in jump.GetModdedConstraints() ?? Enumerable.Empty<ExtraJump.Position>())
            {
                if (!after.ContainsKey(position.Target)) continue;
                if (position is ExtraJump.After) after[jump].Add(position.Target);
                else if (position is ExtraJump.Before) after[position.Target].Add(jump);
            }
        }
        var result = new List<ExtraJump>();
        var left = new List<ExtraJump>(elements);
        while (left.Count > 0)
        {
            ExtraJump? next = left.FirstOrDefault(j => after[j].All(result.Contains));
            if (next == null) throw new ArgumentException($"ExtraJump ordering has a cycle: {string.Join(", ", left)}");
            result.Add(next);
            left.Remove(next);
        }
        return result;
    }

    internal static PlayerJumps Of(Player player)
    {
        PlayerJumps? jumps = Players.Get(player.Pointer);
        int count = ExtraJumpCount;
        if (jumps != null && jumps.States.Length == count) return jumps;
        jumps ??= new PlayerJumps();
        Array.Resize(ref jumps.States, count);
        Array.Resize(ref jumps.SnapEnabled, count);
        Array.Resize(ref jumps.SnapAvailable, count);
        Array.Resize(ref jumps.SnapActive, count);
        Players.Set(player.Pointer, jumps);
        for (int i = 0; i < DefaultExtraJumpCount; i++) Pull(player, jumps, i);
        return jumps;
    }

    static VanillaExtraJump? NativeAt(int index) =>
        index < DefaultExtraJumpCount && ExtraJumps[index] is VanillaExtraJump { Native: true } v ? v : null;

    static void Push(Player player, PlayerJumps jumps, int index)
    {
        if (NativeAt(index) is not { } jump) return;
        ref ExtraJumpState state = ref jumps.States[index];
        if (state._disabled)
        {
            jump.HasOption(player) = false;
            jump.CanJumpAgain(player) = false;
        }
        else if (state._enabled && !jumps.SnapEnabled[index]) jump.HasOption(player) = true;
        if (state._available != jumps.SnapAvailable[index]) jump.CanJumpAgain(player) = state._available;
        if (state._active != jumps.SnapActive[index]) jump.IsPerforming(player) = state._active;
    }

    static void Pull(Player player, PlayerJumps jumps, int index)
    {
        if (NativeAt(index) is not { } jump) return;
        ref ExtraJumpState state = ref jumps.States[index];
        state._enabled = jumps.SnapEnabled[index] = jump.HasOption(player);
        state._available = jumps.SnapAvailable[index] = jump.CanJumpAgain(player);
        state._active = jumps.SnapActive[index] = jump.IsPerforming(player);
    }

    internal static void Sync(Player player, PlayerJumps jumps)
    {
        for (int i = 0; i < DefaultExtraJumpCount; i++)
        {
            Push(player, jumps, i);
            Pull(player, jumps, i);
        }
    }

    internal static void PullAll(Player player, PlayerJumps jumps)
    {
        for (int i = 0; i < DefaultExtraJumpCount; i++) Pull(player, jumps, i);
    }

    internal static ref ExtraJumpState StateOf(Player player, ExtraJump jump)
    {
        PlayerJumps jumps = Of(player);
        if (jump.Type < DefaultExtraJumpCount)
        {
            Push(player, jumps, jump.Type);
            Pull(player, jumps, jump.Type);
        }
        return ref jumps.States[jump.Type];
    }

    internal static Span<ExtraJumpState> StatesOf(Player player)
    {
        PlayerJumps jumps = Of(player);
        Sync(player, jumps);
        return jumps.States.AsSpan();
    }

    internal static bool CanStart(Player player, ExtraJump jump) =>
        jump.CanStart(player) && PlayerLoader.All(player, nameof(ModPlayer.CanStartExtraJump), jump, static (m, j) => m.CanStartExtraJump(j));

    internal static bool AnyUsable(Player player)
    {
        PlayerJumps jumps = Of(player);
        if (jumps.Block) return false;
        Sync(player, jumps);
        foreach (ExtraJump jump in OrderedJumps)
        {
            if (jumps.States[jump.Type].Available && CanStart(player, jump)) return true;
        }
        return false;
    }

    public static void UpdateHorizontalSpeeds(Player player)
    {
        if (!HasModded) return;
        PlayerJumps jumps = Of(player);
        foreach (ExtraJump jump in OrderedJumps)
        {
            if (jump.Type >= DefaultExtraJumpCount && jumps.States[jump.Type].Active) jump.UpdateHorizontalSpeeds(player);
        }
    }

    internal static void ModdedVisuals(Player player, PlayerJumps jumps)
    {
        foreach (ExtraJump jump in OrderedJumps)
        {
            if (jump.Type < DefaultExtraJumpCount || !jumps.States[jump.Type].Active || !jump.CanShowVisuals(player)) continue;
            if (!PlayerLoader.All(player, nameof(ModPlayer.CanShowExtraJumpVisuals), jump, static (m, j) => m.CanShowExtraJumpVisuals(j))) continue;
            jump.ShowVisuals(player);
            PlayerLoader.Each(player, nameof(ModPlayer.ExtraJumpVisuals), jump, static (m, j) => m.ExtraJumpVisuals(j));
        }
    }

    internal static void RefreshModded(Player player, PlayerJumps jumps)
    {
        foreach (ExtraJump jump in OrderedJumps)
        {
            if (jump.Type < DefaultExtraJumpCount || !jumps.States[jump.Type].Enabled) continue;
            jump.OnRefreshed(player);
            PlayerLoader.Each(player, nameof(ModPlayer.OnExtraJumpRefreshed), jump, static (m, j) => m.OnExtraJumpRefreshed(j));
            jumps.States[jump.Type].Available = true;
        }
    }

    internal static void StopActiveJump(Player player, out bool anyJumpCancelled)
    {
        anyJumpCancelled = false;
        PlayerJumps jumps = Of(player);
        Sync(player, jumps);
        foreach (ExtraJump jump in OrderedJumps)
        {
            if (!jumps.States[jump.Type].Active) continue;
            StopJump(jump, player, jumps);
            anyJumpCancelled = true;
        }
        Sync(player, jumps);
    }

    internal static void StopModdedJumps(Player player, PlayerJumps jumps)
    {
        for (int i = DefaultExtraJumpCount; i < jumps.States.Length; i++)
        {
            if (jumps.States[i].Active) StopJump(ExtraJumps[i], player, jumps);
        }
    }

    internal static void ResetEnableFlags(Player player, PlayerJumps jumps)
    {
        jumps.Block = false;
        for (int i = 0; i < jumps.States.Length; i++) jumps.States[i].ResetEnabled();
        PullAll(player, jumps);
    }

    internal static void ConsumeAndStopUnavailableModded(Player player, PlayerJumps jumps)
    {
        for (int i = DefaultExtraJumpCount; i < jumps.States.Length; i++)
        {
            ref ExtraJumpState state = ref jumps.States[i];
            if (state._available && !state.Enabled) Cleared(player, ExtraJumps[i]);
            state.CommitEnabledState(out bool jumpEnded);
            if (!jumpEnded) continue;
            StopJump(ExtraJumps[i], player, jumps);
            player.jump = 0;
        }
    }

    internal static void ConsumeModded(Player player, PlayerJumps jumps)
    {
        for (int i = DefaultExtraJumpCount; i < jumps.States.Length; i++)
        {
            if (!jumps.States[i].Available) continue;
            Cleared(player, ExtraJumps[i]);
            jumps.States[i].Available = false;
        }
    }

    internal static void ConsumeAllJumps(Player player)
    {
        PlayerJumps jumps = Of(player);
        Sync(player, jumps);
        foreach (ExtraJump jump in ExtraJumps)
        {
            if (!jumps.States[jump.Type].Available) continue;
            Cleared(player, jump);
            jumps.States[jump.Type].Available = false;
        }
        Sync(player, jumps);
    }

    internal static void Cleared(Player player, ExtraJump jump) =>
        PlayerLoader.Each(player, nameof(ModPlayer.OnExtraJumpCleared), jump, static (m, j) => m.OnExtraJumpCleared(j));

    internal static float Duration(Player player, ExtraJump jump)
    {
        float duration = jump.GetDurationMultiplier(player);
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyExtraJumpDurationMultiplier), jump, ref duration,
            static (m, j, ref d) => m.ModifyExtraJumpDurationMultiplier(j, ref d));
        return duration;
    }

    internal static bool Started(Player player, ExtraJump jump, bool vanilla)
    {
        bool playSound = true;
        if (!vanilla) jump.OnStarted(player, ref playSound);
        var box = new System.Runtime.CompilerServices.StrongBox<bool>(playSound);
        PlayerLoader.Each(player, nameof(ModPlayer.OnExtraJumpStarted), (jump, box), static (m, s) => m.OnExtraJumpStarted(s.jump, ref s.box.Value));
        return box.Value;
    }

    internal static void PerformJump(ExtraJump jump, Player player, PlayerJumps jumps)
    {
        jumps.States[jump.Type].Start();
        float duration = Duration(player, jump);
        player.velocity.Y = -Player.jumpSpeed * player.gravDir;
        player.jump = (int)(Player.jumpHeight * duration);
        if (Started(player, jump, false)) SoundEngine.PlaySound(16, (int)player.position.X, (int)player.position.Y);
    }

    static void StopJump(ExtraJump jump, Player player, PlayerJumps jumps)
    {
        if (jump.Type >= DefaultExtraJumpCount) jump.OnEnded(player);
        PlayerLoader.Each(player, nameof(ModPlayer.OnExtraJumpEnded), jump, static (m, j) => m.OnExtraJumpEnded(j));
        jumps.States[jump.Type].Stop();
    }

    internal static void Ended(Player player, ExtraJump jump) =>
        PlayerLoader.Each(player, nameof(ModPlayer.OnExtraJumpEnded), jump, static (m, j) => m.OnExtraJumpEnded(j));

    internal static int DefaultCount => DefaultExtraJumpCount;
}
