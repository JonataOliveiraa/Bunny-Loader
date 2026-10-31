using System;
using CsProjectile.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsProjectile;

public static class Checks
{
    static int pass, fail, frames, bolt = -1, fuse = -1, last = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsProjectileMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        bolt = fuse = last = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 30) Spawn(p);
            if (frames == 90)
            {
                last = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(30f, -120f), new Vector2(0f, 6f), ModContent.ProjectileType<TestBolt>(), 10, 1f, p.whoAmI);
                Main.projectile[last].penetrate = 1;
            }
            if (frames != 240) return;
            done = true;
            Run(p);
        }
        catch (Exception e)
        {
            done = true;
            Report("run", false, e.ToString());
        }
        if (done) Log.Info($"csprojectile END pass={pass} fail={fail}");
    }

    static void Spawn(Player p)
    {
        bolt = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -120f), new Vector2(0f, 6f), ModContent.ProjectileType<TestBolt>(), 10, 1f, p.whoAmI);
        fuse = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -200f), Vector2.Zero, ModContent.ProjectileType<TestFuse>(), 0, 0f, p.whoAmI);
        Projectile shot = Main.projectile[bolt];
        Report("NewProjectile makes the mod projectile with its SetDefaults", shot.type == ModContent.ProjectileType<TestBolt>() && shot.ModProjectile is TestBolt &&
            shot.width == 8 && shot.penetrate == 3 && shot.active && shot.DamageType == DamageClass.Ranged, $"type={shot.type} width={shot.width} penetrate={shot.penetrate} active={shot.active}");
        Report("OnSpawn gets the spawn source", Counts.Of("OnSpawn") == 1 && TestBolt.SpawnSource.Pointer != IntPtr.Zero, $"calls={Counts.Of("OnSpawn")}");
    }

    static void Run(Player p)
    {
        int type = ModContent.ProjectileType<TestBolt>();
        Report("the type comes after the game's projectiles", type >= 1136 && ProjectileLoader.GetProjectile(type) == ModContent.GetInstance<TestBolt>() &&
            TestBolt.StaticDefaults == 1 && ProjectileID.Sets.TrailCacheLength[type] == 5, $"type={type} static={TestBolt.StaticDefaults}");
        Report("PreAI, AI and PostAI run every update", Counts.Of("PreAI") >= 20 && Counts.Of("AI") == Counts.Of("PreAI") && Counts.Of("PostAI") == Counts.Of("PreAI"),
            $"pre={Counts.Of("PreAI")} ai={Counts.Of("AI")} post={Counts.Of("PostAI")}");
        Report("OnTileCollide false keeps it bouncing", TestBolt.Bounces >= 1, $"bounces={TestBolt.Bounces} collisions={Counts.Of("OnTileCollide")}");
        Report("the mod's Kill from OnTileCollide reaches OnKill", TestBolt.KilledByPenetrate && Counts.Of("OnKill") == 1 && !Main.projectile[last].active,
            $"killed={TestBolt.KilledByPenetrate} onKill={Counts.Of("OnKill")} active={Main.projectile[last].active}");
        Report("PreDraw and PostDraw run while it is on screen", Counts.Of("PreDraw") >= 5 && Counts.Of("PostDraw") == Counts.Of("PreDraw") && TestBolt.TrailLength == 5,
            $"pre={Counts.Of("PreDraw")} post={Counts.Of("PostDraw")} trail={TestBolt.TrailLength}");
        Report("PreKill false skips OnKill", Counts.Of("FusePreKill") == 1 && Counts.Of("FuseOnKill") == 0 && !Main.projectile[fuse].active,
            $"pre={Counts.Of("FusePreKill")} on={Counts.Of("FuseOnKill")} active={Main.projectile[fuse].active}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csprojectile {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
