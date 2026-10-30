using System;
using CsGrapple.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGrapple;

public static class Checks
{
    static int pass, fail, frames, ranged = -1, vanilla = -1, a = -1, b = -1;
    static int[] more = Array.Empty<int>();
    static float rangedMax, vanillaMax, rangedSpeed, vanillaSpeed;
    static int rangedBack, vanillaBack;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsGrappleMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        ranged = vanilla = a = b = -1;
        rangedMax = vanillaMax = rangedSpeed = vanillaSpeed = 0f;
        rangedBack = vanillaBack = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) Fire(p);
            if (frames == 62) StartRange(p);
            if (frames > 62 && frames < 150) Track(p);
            if (frames == 150) CheckRange();
            if (frames == 160) StartLatch(p);
            if (frames == 164) CheckLatch(p);
            if (frames == 168) CheckLimit(p);
            if (frames == 172) CheckRaisedLimit(p);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            KillAll(p);
            Log.Info($"csgrapple END pass={pass} fail={fail}");
        }
    }

    static void Fire(Player p)
    {
        int hook = ModContent.ProjectileType<TestHook>(), other = ModContent.ProjectileType<OtherHook>();
        KillAll(p);
        TestHook.CanUse = false;
        Shoot(p, hook);
        Report("CanUseGrapple false stops the shot", Count(p, hook) == 0 && TestHook.CanUseCalls >= 1, $"hooks={Count(p, hook)} calls={TestHook.CanUseCalls}");

        TestHook.CanUse = null;
        TestHook.SwapTo = other;
        Shoot(p, hook);
        Report("UseGrapple changes the projectile type", Count(p, other) == 1 && Count(p, hook) == 0, $"other={Count(p, other)} hooks={Count(p, hook)}");
        TestHook.SwapTo = 0;
        KillAll(p);

        TestHook.CanUse = true;
        for (int i = 0; i < 5; i++) Shoot(p, hook);
        int forced = Count(p, hook);
        KillAll(p);
        TestHook.CanUse = null;
        for (int i = 0; i < 5; i++) Shoot(p, hook);
        int limited = Count(p, hook);
        KillAll(p);
        Report("CanUseGrapple true skips the game's flying limit", forced == 5 && limited == 3, $"forced={forced} limited={limited}");

        HookGlobal.BlockVanilla = true;
        Shoot(p, ProjectileID.Hook);
        int blocked = Count(p, ProjectileID.Hook);
        HookGlobal.BlockVanilla = false;
        Shoot(p, ProjectileID.Hook);
        int allowed = Count(p, ProjectileID.Hook);
        KillAll(p);
        Report("GlobalProjectile.CanUseGrapple applies to its game hook", blocked == 0 && allowed == 1, $"blocked={blocked} allowed={allowed}");
    }

    static void StartRange(Player p)
    {
        TestHook.Range = 96f;
        TestHook.Retreat = 30f;
        TestHook.Latch = false;
        ranged = Projectile.NewProjectile(p.GetSource_FromThis(), p.MountedCenter, new Vector2(0f, -10f), ModContent.ProjectileType<TestHook>(), 0, 0f, p.whoAmI);
        vanilla = Projectile.NewProjectile(p.GetSource_FromThis(), p.MountedCenter, new Vector2(0f, -10f), ProjectileID.Hook, 0, 0f, p.whoAmI);
    }

    static void Track(Player p)
    {
        Sample(p, ranged, ref rangedMax, ref rangedSpeed, ref rangedBack);
        Sample(p, vanilla, ref vanillaMax, ref vanillaSpeed, ref vanillaBack);
    }

    static void Sample(Player p, int index, ref float max, ref float speed, ref int back)
    {
        if ((uint)index >= 1000u) return;
        Projectile hook = Main.projectile[index];
        if (!hook.active) return;
        if (hook.ai[0] == 0f) max = Math.Max(max, Vector2.Distance(p.MountedCenter, hook.Center));
        else if (hook.ai[0] == 1f && ++back == 2) speed = hook.velocity.Length();
    }

    static void CheckRange()
    {
        Report("GrappleRange retracts the mod hook", rangedMax > 80f && rangedMax <= 108f, $"max={rangedMax}");
        Report("game hooks keep their own range", vanillaMax > 280f && vanillaMax <= 312f, $"max={vanillaMax}");
        Report("GrappleRetreatSpeed on ModProjectile and GlobalProjectile", Math.Abs(rangedSpeed - 30f) < 0.5f && Math.Abs(vanillaSpeed - 40f) < 0.5f && HookGlobal.RetreatCalls > 0,
            $"mod={rangedSpeed} global={vanillaSpeed} calls={HookGlobal.RetreatCalls}");
    }

    static void StartLatch(Player p)
    {
        KillAll(p);
        TestHook.Latch = true;
        TestHook.Limit = 1;
        TestHook.Pull = 5f;
        TestHook.OffsetX = 64f;
        a = Spawn(p, new Vector2(-48f, -96f));
    }

    static void CheckLatch(Player p)
    {
        Projectile hook = Main.projectile[a];
        Point tile = hook.Center.ToTileCoordinates();
        Report("GrappleCanLatchOnTo gets the tile coordinates and can latch", hook.active && hook.ai[0] == 2f && TestHook.LatchX == tile.X && TestHook.LatchY == tile.Y,
            $"state={hook.ai[0]} latch=({TestHook.LatchX},{TestHook.LatchY}) tile=({tile.X},{tile.Y})");

        p.grapCount = 1;
        p.grappling[0] = a;
        Vector2 from = p.Center;
        p.GetGrapplingForces(from, out _, out float vx, out float vy);
        float tx = hook.position.X + hook.width / 2 + 64f - from.X, ty = hook.position.Y + hook.height / 2 - from.Y;
        float length = MathF.Sqrt(tx * tx + ty * ty), scale = length <= 5f ? 1f : 5f / length;
        Report("GrappleTargetPoint and GrapplePullSpeed drive the pull", Math.Abs(vx - tx * scale) < 0.01f && Math.Abs(vy - ty * scale) < 0.01f,
            $"v=({vx},{vy}) expected=({tx * scale},{ty * scale})");
        p.grapCount = 0;

        b = Spawn(p, new Vector2(48f, -96f));
    }

    static void CheckLimit(Player p)
    {
        Projectile first = Main.projectile[a], second = Main.projectile[b];
        Report("NumGrappleHooks lowers the latched limit", !first.active && second.active && second.ai[0] == 2f,
            $"first={first.active} second={second.active}/{second.ai[0]}");
        TestHook.Limit = 5;
        more = new[] { Spawn(p, new Vector2(-96f, -160f)), Spawn(p, new Vector2(0f, -160f)), Spawn(p, new Vector2(96f, -160f)) };
    }

    static void CheckRaisedLimit(Player p)
    {
        int latched = Main.projectile[b].active && Main.projectile[b].ai[0] == 2f ? 1 : 0;
        foreach (int index in more)
            if (Main.projectile[index].active && Main.projectile[index].ai[0] == 2f) latched++;
        Report("NumGrappleHooks raises the latched limit", latched == 4, $"latched={latched}");
        done = true;
    }

    static int Spawn(Player p, Vector2 offset) =>
        Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + offset, new Vector2(0f, -1f), ModContent.ProjectileType<TestHook>(), 0, 0f, p.whoAmI);

    static void Shoot(Player p, int type)
    {
        var item = new Item(ItemID.GrapplingHook);
        item.shoot = type;
        p.FireGrapple(item);
    }

    static int Count(Player p, int type)
    {
        int n = 0;
        for (int i = 0; i < 1000; i++)
            if (Main.projectile[i].active && Main.projectile[i].owner == p.whoAmI && Main.projectile[i].type == type) n++;
        return n;
    }

    static void KillAll(Player p)
    {
        for (int i = 0; i < 1000; i++)
        {
            Projectile hook = Main.projectile[i];
            if (hook.active && hook.owner == p.whoAmI && hook.aiStyle == ProjAIStyleID.Hook) hook.Kill();
        }
        p.grapCount = 0;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csgrapple {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
