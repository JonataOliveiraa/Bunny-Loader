using System;
using System.IO;
using BunnyLoader;
using CsPerf;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsMpPerf;

public sealed class CsMpPerfMod : Mod
{
    const int Window = 300, Projectiles = 60, Npcs = 15;

    static int frames, joinedAt, projType, npcType, joinTries;
    static bool done, entities, joining;
    static DateTime titleSince, nextJoin;
    static BunnyLoader.Interop.GameHandle? keep;

    public override void Load()
    {
        Measure.Install(Logger, "csmpperf");
        On_Player.Update += Update;
        On_Main.DoUpdate += DoUpdate;
        On_NPC.SpawnNPC += orig => { };
        On_NPC_Spawner.SpawnNPC += (orig, self) => { };
    }

    static string ClientFile => Path.Combine(Main.PlayerPath ?? "", "csmpperf.client");

    static int Players()
    {
        int count = 0;
        for (int i = 0; i < 255; i++)
            if (Main.player[i].active) count++;
        return count;
    }

    static string Mode => Main.netMode switch { 0 => "single", 1 => "client", _ => "host" };

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (joining || Main.netMode != 0 || !Main.gameMenu || Main.menuMode != 0 || DateTime.UtcNow < nextJoin) return;
        string? player;
        try { player = File.Exists(ClientFile) ? File.ReadAllText(ClientFile).Trim() : null; }
        catch { player = null; }
        if (string.IsNullOrEmpty(player)) return;
        if (titleSince == default) titleSince = DateTime.UtcNow;
        if (DateTime.UtcNow - titleSince < TimeSpan.FromSeconds(25)) return;
        nextJoin = DateTime.UtcNow.AddSeconds(8);
        if (++joinTries > 6) return;
        try
        {
            Main.LoadPlayers(false);
            var list = new BunnyLoader.Interop.GameList(Main.PlayerList);
            PlayerFileData data = default;
            for (int i = 0; i < list.Count; i++)
            {
                var candidate = new PlayerFileData(list.Get<IntPtr>(i));
                if (Path.GetFileNameWithoutExtension(candidate.Path ?? "") == player) data = candidate;
            }
            if (data.IsNull)
            {
                Measure.Info($"no player {player}");
                return;
            }
            Main.SelectPlayer(data);
            var buffers = NetMessage.buffer;
            if (buffers[256].IsNull)
            {
                MessageBuffer created = MessageBuffer.New();
                created.whoAmI = 256;
                keep = new BunnyLoader.Interop.GameHandle(created.Pointer);
                buffers[256] = created;
            }
            Main.netMode = 1;
            Netplay.ListenPort = 7777;
            Netplay.ServerPassword = "";
            Main.getIP = "127.0.0.1";
            Netplay.SetRemoteIP("127.0.0.1");
            MPSessionManager sessions = MPSessionManager.Instance;
            if (!sessions.IsNull)
                sessions.CurrentSession = sessions.CreateNewDirectSession(PlatformUser.CurrentUser, MPSessionManager.LobbyState.InProgress, "127.0.0.1", 7777);
            Main.menuMode = 10;
            Netplay.StartTcpClient(false);
            Measure.Info($"joining 127.0.0.1:7777 as {player} (try {joinTries})");
        }
        catch (Exception e) { Measure.Info($"join failed {e}"); }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (!Main.gameMenu && i == Main.myPlayer)
        {
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
            self.breath = self.breathMax;
        }
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.netMode == 1) joining = true;
        try
        {
            frames++;
            if (Main.netMode == 1) ClientStep(frames);
            else HostStep(self, frames);
        }
        catch (Exception e) { Measure.Info($"error {e}"); }
    }

    static string Counts()
    {
        int p = 0, n = 0;
        foreach (Projectile q in Main.projectile)
            if (q.active) p++;
        foreach (NPC m in Main.npc)
            if (m.active) n++;
        return $"players={Players()} projectiles={p} npcs={n}";
    }

    static void ClientStep(int f)
    {
        switch (f)
        {
            case 60:
                Measure.Info($"client in the world, profiler {(HookProfile.Enabled ? "on" : "OFF")}, {Counts()}");
                break;
            case 240:
                Measure.Begin($"client 1 ({Counts()})");
                break;
            case 240 + Window:
                Measure.End();
                break;
            case 660:
                Measure.Begin($"client 2 ({Counts()})");
                break;
            case 660 + Window:
                Measure.End();
                Measure.Info($"client done, {Counts()}");
                Measure.Info("END");
                done = true;
                break;
        }
    }

    static void HostStep(Player p, int f)
    {
        if (entities && projType > 0) KeepProjectiles(p);
        if (f == 60)
        {
            projType = ModContent.TryFind("ExampleMod/SparklingBall", out ModProjectile proj) ? proj.Type : 0;
            npcType = ModContent.TryFind("ExampleMod/ExampleCustomAISlimeNPC", out ModNPC npc) ? npc.Type : 0;
            Measure.Info($"{Mode} in the world, profiler {(HookProfile.Enabled ? "on" : "OFF")}, bias {HookProfile.BiasNs:0} ns, projectile {projType}, npc {npcType}, {Counts()}");
        }
        else if (f == 120) Measure.Begin($"{Mode} alone");
        else if (f == 120 + Window) Measure.End();
        if (f <= 120 + Window) return;
        if (joinedAt == 0)
        {
            if (Main.netMode == 0 || Players() >= 2 || f > 120 + Window + 7200)
            {
                joinedAt = f;
                Measure.Info($"{Mode} step 2 at frame {f}, {Counts()}");
            }
            return;
        }
        int g = f - joinedAt;
        if (g == 240) Measure.Begin($"{Mode} ({Counts()})");
        else if (g == 240 + Window)
        {
            Measure.End();
            entities = true;
            if (npcType > 0)
                for (int k = 0; k < Npcs; k++)
                    NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + (k - 7) * 40, (int)p.Center.Y - 200, npcType);
        }
        else if (g == 660) Measure.Begin($"{Mode} entities ({Counts()})");
        else if (g == 660 + Window)
        {
            Measure.End();
            Measure.Info($"{Mode} done, {Counts()}");
            entities = false;
            foreach (Projectile q in Main.projectile)
                if (q.active && q.type == projType) q.active = false;
            foreach (NPC n in Main.npc)
                if (n.active && n.type == npcType) n.active = false;
            Measure.Info("END");
            done = true;
        }
    }

    static void KeepProjectiles(Player p)
    {
        int alive = 0;
        foreach (Projectile q in Main.projectile)
        {
            if (!q.active || q.type != projType) continue;
            q.timeLeft = 600;
            q.velocity = Vector2.Zero;
            alive++;
        }
        for (int k = alive; k < Projectiles; k++)
            Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(k % 10 * 30 - 150, -120 - k / 10 * 30), Vector2.Zero, projType, 0, 0f, p.whoAmI);
    }
}
