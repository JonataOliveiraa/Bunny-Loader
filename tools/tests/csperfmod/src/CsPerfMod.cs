using System;
using BunnyLoader;
using CsPerf;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsPerfMod;

public sealed class CsPerfModMod : Mod
{
    const int Projectiles = 60, Npcs = 15;

    static int frames, projType, npcType;

    public override void Load()
    {
        Measure.Install(Logger, "csperfmod");
        On_Player.Update += Update;
        On_NPC.SpawnNPC += orig => { };
        On_NPC_Spawner.SpawnNPC += (orig, self) => { };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (!Main.gameMenu && i == Main.myPlayer)
        {
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
        }
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer) return;
        try { Step(self, ++frames); }
        catch (Exception e) { Measure.Info($"error {e}"); }
    }

    static void Step(Player p, int f)
    {
        if (f >= 420 && projType > 0) KeepProjectiles(p);
        switch (f)
        {
            case 60:
                projType = ModContent.TryFind("ExampleMod/SparklingBall", out ModProjectile proj) ? proj.Type : 0;
                npcType = ModContent.TryFind("ExampleMod/ExampleCustomAISlimeNPC", out ModNPC npc) ? npc.Type : 0;
                Measure.Info($"profiler {(HookProfile.Enabled ? "on" : "OFF")}, bias {HookProfile.BiasNs:0} ns, clock {HookProfile.ClockNs:0} ns, projectile {projType}, npc {npcType}, systems {PerfSystem.Calls}");
                break;
            case 120:
                Measure.Begin("idle");
                break;
            case 420:
                Measure.End();
                if (npcType > 0)
                    for (int k = 0; k < Npcs; k++)
                        NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + (k - 7) * 40, (int)p.Center.Y - 200, npcType);
                break;
            case 540:
                Measure.Begin($"{Projectiles} projectiles + {Npcs} NPCs");
                break;
            case 840:
                Measure.End();
                int liveProjectiles = 0, liveNpcs = 0;
                var projectiles = Main.projectile;
                for (int k = 0; k < projectiles.Length; k++)
                {
                    Projectile q = projectiles[k];
                    if (q.active && q.type == projType) { liveProjectiles++; q.active = false; }
                }
                var npcs = Main.npc;
                for (int k = 0; k < npcs.Length; k++)
                {
                    NPC n = npcs[k];
                    if (n.active && n.type == npcType) { liveNpcs++; n.active = false; }
                }
                Measure.Info($"alive at the end: {liveProjectiles} projectiles, {liveNpcs} NPCs, system hook calls {PerfSystem.Calls}");
                Measure.Info("END");
                break;
        }
    }

    static void KeepProjectiles(Player p)
    {
        int alive = 0;
        var projectiles = Main.projectile;
        for (int i = 0; i < projectiles.Length; i++)
        {
            Projectile q = projectiles[i];
            if (!q.active || q.type != projType) continue;
            q.timeLeft = 600;
            q.velocity = Vector2.Zero;
            alive++;
        }
        for (int k = alive; k < Projectiles; k++)
            Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(k % 10 * 30 - 150, -120 - k / 10 * 30), Vector2.Zero, projType, 0, 0f, p.whoAmI);
    }
}

public sealed class PerfSystem : ModSystem
{
    public static long Calls;

    public override void PreUpdateEntities() => Calls++;
    public override void PreUpdatePlayers() => Calls++;
    public override void PostUpdatePlayers() => Calls++;
    public override void PreUpdateNPCs() => Calls++;
    public override void PostUpdateNPCs() => Calls++;
    public override void PreUpdateGores() => Calls++;
    public override void PostUpdateGores() => Calls++;
    public override void PreUpdateProjectiles() => Calls++;
    public override void PostUpdateProjectiles() => Calls++;
    public override void PreUpdateItems() => Calls++;
    public override void PostUpdateItems() => Calls++;
    public override void PreUpdateDusts() => Calls++;
    public override void PostUpdateDusts() => Calls++;
    public override void PreUpdateTime() => Calls++;
    public override void PostUpdateTime() => Calls++;
    public override void PreUpdateWorld() => Calls++;
    public override void PostUpdateWorld() => Calls++;
    public override void PreUpdateInvasions() => Calls++;
    public override void PostUpdateInvasions() => Calls++;
    public override void PostUpdateEverything() => Calls++;
    public override void UpdateUI(GameTime gameTime) => Calls++;
    public override void PostUpdateInput() => Calls++;
}
