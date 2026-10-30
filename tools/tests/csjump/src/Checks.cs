using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsJump;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsJumpMod>().Logger;

    static int Calls(string name) => TestJump.Calls.GetValueOrDefault(name);

    static bool Logged(string entry) => JumpPlayer.Log.Contains(entry);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        float x = p.position.X, y = p.position.Y;
        foreach (Action<Player> step in new Action<Player>[] { Order, ModJump, Vanilla, States, Cleared })
        {
            try { step(p); }
            catch (Exception e) { Report(step.Method.Name, false, e.ToString()); }
        }
        p.jump = 0;
        p.velocity = default;
        p.position.X = x;
        p.position.Y = y;
        Log.Info($"csjump END pass={pass} fail={fail}");
    }

    static void NoVanilla(Player p)
    {
        p.hasJumpOption_Cloud = p.canJumpAgain_Cloud = p.isPerformingJump_Cloud = false;
        p.hasJumpOption_Sandstorm = p.canJumpAgain_Sandstorm = p.isPerformingJump_Sandstorm = false;
        p.hasJumpOption_Blizzard = p.canJumpAgain_Blizzard = p.isPerformingJump_Blizzard = false;
        p.hasJumpOption_Fart = p.canJumpAgain_Fart = p.isPerformingJump_Fart = false;
        p.hasJumpOption_Sail = p.canJumpAgain_Sail = p.isPerformingJump_Sail = false;
    }

    static void Press(Player p)
    {
        p.jump = 0;
        p.velocity.Y = 1f;
        p.controlJump = true;
        p.releaseJump = true;
        p.sliding = false;
    }

    static void Order(Player p)
    {
        var names = ExtraJumpLoader.OrderedJumps.Select(j => j.Name).ToList();
        int test = names.IndexOf(nameof(TestJump));
        Report("TestJump sits after the blizzard jump", test == names.IndexOf(nameof(BlizzardInABottleJump)) + 1 && ExtraJumpLoader.ExtraJumpCount == 11,
            string.Join(",", names));
    }

    static void ModJump(Player p)
    {
        NoVanilla(p);
        p.GetJumpState<TestJump>().Enable();
        p.RefreshDoubleJumps();
        Report("RefreshDoubleJumps refreshes the mod jump", Calls(nameof(TestJump.OnRefreshed)) == 1 && Logged("OnExtraJumpRefreshed:TestJump") &&
            p.GetJumpState<TestJump>().Available, $"calls={Calls(nameof(TestJump.OnRefreshed))} available={p.GetJumpState<TestJump>().Available}");

        JumpPlayer.TestFactor = 1.5f;
        Press(p);
        p.JumpMovement();
        JumpPlayer.TestFactor = 1f;
        int expected = (int)(Player.jumpHeight * 2f * 1.5f);
        Report("JumpMovement starts the mod jump", p.GetJumpState<TestJump>().Active && !p.GetJumpState<TestJump>().Available &&
            Calls(nameof(TestJump.OnStarted)) == 1 && Logged("OnExtraJumpStarted:TestJump") && Logged("CanStartExtraJump:TestJump"),
            $"active={p.GetJumpState<TestJump>().Active} started={Calls(nameof(TestJump.OnStarted))}");
        Report("the duration multiplier sets the jump time", p.jump == expected || p.jump == expected - 1 && p.velocity.Y < 0,
            $"jump={p.jump} expected={expected} vy={p.velocity.Y}");

        float speed = p.maxRunSpeed;
        p.HorizontalMovement();
        Report("UpdateHorizontalSpeeds runs while active", Calls(nameof(TestJump.UpdateHorizontalSpeeds)) >= 1, $"calls={Calls(nameof(TestJump.UpdateHorizontalSpeeds))} speed={speed}");

        p.DoubleJumpVisuals();
        Report("ShowVisuals and ExtraJumpVisuals run while rising", Calls(nameof(TestJump.ShowVisuals)) == 1 && Logged("ExtraJumpVisuals:TestJump"),
            $"calls={Calls(nameof(TestJump.ShowVisuals))}");

        p.jump = 0;
        p.CancelAllJumpVisualEffects(false);
        Report("CancelAllJumpVisualEffects ends the mod jump", !p.GetJumpState<TestJump>().Active && Calls(nameof(TestJump.OnEnded)) == 1 &&
            Logged("OnExtraJumpEnded:TestJump"), $"active={p.GetJumpState<TestJump>().Active} ended={Calls(nameof(TestJump.OnEnded))}");
    }

    static void Vanilla(Player p)
    {
        NoVanilla(p);
        p.GetJumpState<TestJump>().Enable();
        p.GetJumpState<TestJump>().Available = true;
        p.hasJumpOption_Cloud = p.canJumpAgain_Cloud = true;
        Press(p);
        p.JumpMovement();
        Report("the mod jump comes before the cloud", p.GetJumpState<TestJump>().Active && p.canJumpAgain_Cloud && !p.isPerformingJump_Cloud,
            $"test={p.GetJumpState<TestJump>().Active} cloud={p.canJumpAgain_Cloud}/{p.isPerformingJump_Cloud}");
        p.jump = 0;
        p.CancelAllJumpVisualEffects(false);

        JumpPlayer.VetoCloud = true;
        Press(p);
        p.JumpMovement();
        JumpPlayer.VetoCloud = false;
        Report("CanStartExtraJump false keeps the cloud", p.canJumpAgain_Cloud && !p.isPerformingJump_Cloud && Logged("CanStartExtraJump:CloudInABottleJump"),
            $"cloud={p.canJumpAgain_Cloud}/{p.isPerformingJump_Cloud} jump={p.jump}");

        JumpPlayer.CloudFactor = 2f;
        Press(p);
        p.JumpMovement();
        JumpPlayer.CloudFactor = 1f;
        int expected = (int)(Player.jumpHeight * 0.75f * 2f);
        Report("the cloud jump starts with the hooks", p.isPerformingJump_Cloud && !p.canJumpAgain_Cloud && Logged("OnExtraJumpStarted:CloudInABottleJump"),
            $"cloud={p.canJumpAgain_Cloud}/{p.isPerformingJump_Cloud}");
        Report("ModifyExtraJumpDurationMultiplier changes a game jump", p.jump == expected || p.jump == expected - 1, $"jump={p.jump} expected={expected}");

        p.jump = 0;
        p.CancelAllJumpVisualEffects(false);
        Report("OnExtraJumpEnded runs for the cloud", !p.isPerformingJump_Cloud && Logged("OnExtraJumpEnded:CloudInABottleJump"), $"cloud={p.isPerformingJump_Cloud}");
    }

    static void States(Player p)
    {
        NoVanilla(p);
        p.GetJumpState(ExtraJump.FartInAJar).Enable();
        p.UpdateJumpHeight();
        bool enabled = p.hasJumpOption_Fart;
        p.hasJumpOption_Sandstorm = true;
        bool seen = p.GetJumpState(ExtraJump.SandstormInABottle).Enabled;
        p.GetJumpState(ExtraJump.SandstormInABottle).Disable();
        p.UpdateJumpHeight();
        bool disabled = !p.hasJumpOption_Sandstorm;
        Report("GetJumpState of a game jump reads and writes the game's fields", enabled && seen && disabled,
            $"fartEnabled={enabled} sandstormSeen={seen} sandstormDisabled={disabled}");
    }

    static void Cleared(Player p)
    {
        NoVanilla(p);
        p.GetJumpState<TestJump>().Enable();
        p.GetJumpState<TestJump>().Available = true;
        p.hasJumpOption_Cloud = p.canJumpAgain_Cloud = true;
        bool usable = p.AnyExtraJumpUsable();
        p.ConsumeAllExtraJumps();
        Report("ConsumeAllExtraJumps clears every jump", usable && !p.AnyExtraJumpUsable() && !p.canJumpAgain_Cloud &&
            Logged("OnExtraJumpCleared:TestJump") && Logged("OnExtraJumpCleared:CloudInABottleJump"),
            $"usable={usable} after={p.AnyExtraJumpUsable()} cloud={p.canJumpAgain_Cloud}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csjump {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
