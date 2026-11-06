using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsInvasionMp;

public sealed class MpInvasion : ModInvasion
{
    public static int Completed;

    public override IList<InvasionWave> Waves { get; } = new[]
    {
        new InvasionWave(10, new InvasionEnemy(NPCID.BlueSlime)),
        new InvasionWave(5, new InvasionEnemy(NPCID.GreenSlime)),
    };

    public override int SpawnRate => 100000;

    public override void OnComplete() => Completed++;
}

public sealed class CsInvasionMpMod : Mod
{
    static int frame, pass, fail, stage, waited, spawned = -1;
    static bool ended;

    static ILog Log => ModContent.GetInstance<CsInvasionMpMod>().Logger;

    static MpInvasion Test => ModContent.GetInstance<MpInvasion>();

    static bool IsHost => (Main.netMode & NetmodeID.Server) != 0;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csinvasionmp {(IsHost ? "host" : "client")} {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int Players()
    {
        int count = 0;
        for (int k = 0; k < Main.maxPlayers; k++)
            if (Main.player[k].active)
                count++;
        return count;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || ended)
            return;
        frame++;
        try
        {
            if (IsHost) Host(self);
            else Client(self);
        }
        catch (Exception e)
        {
            Report("stage " + stage, false, e.ToString());
        }
    }

    static bool Wait(int frames)
    {
        if (++waited < frames)
            return false;
        waited = 0;
        stage++;
        return true;
    }

    static void End()
    {
        ended = true;
        Log.Info($"csinvasionmp END pass={pass} fail={fail}");
    }

    static void Host(Player self)
    {
        switch (stage)
        {
            case 0:
                if (frame < 30) return;
                Report("the host starts an invasion before the client joins", InvasionLoader.Start(Test) && InvasionLoader.Active == Test, $"max={InvasionLoader.ProgressMax}");
                InvasionLoader.AddPoints(3);
                stage++;
                break;
            case 1:
                if (Players() < 2) return;
                Wait(180);
                break;
            case 2:
                InvasionLoader.AddPoints(2);
                Report("points on the host", InvasionLoader.Progress == 5, $"progress={InvasionLoader.Progress}");
                stage++;
                break;
            case 3:
                if (Wait(120))
                {
                    Player other = default;
                    for (int k = 0; k < Main.maxPlayers; k++)
                        if (Main.player[k].active && k != Main.myPlayer)
                            other = Main.player[k];
                    spawned = NPC.NewNPC(self.GetSource_FromThis(), (int)other.Center.X + 100, (int)other.Center.Y - 40, NPCID.BlueSlime);
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, default, spawned);
                }
                break;
            case 4:
                if (Wait(300))
                    Report("a kill by the client gives points on the host", InvasionLoader.Progress == 6, $"progress={InvasionLoader.Progress}");
                break;
            case 5:
                InvasionLoader.AddPoints(4);
                Report("the wave changes on the host", InvasionLoader.Wave == 1, $"wave={InvasionLoader.Wave} max={InvasionLoader.ProgressMax}");
                stage++;
                break;
            case 6:
                if (Wait(120))
                {
                    Report("the points scale with two players", InvasionLoader.ProgressMax == 8, $"max={InvasionLoader.ProgressMax}");
                    InvasionLoader.AddPoints(8);
                    Report("the invasion completes on the host", InvasionLoader.Active == null && MpInvasion.Completed == 1 && InvasionLoader.IsDowned(Test));
                }
                break;
            case 7:
                if (InvasionLoader.Active == Test)
                {
                    Report("a request from the client starts the invasion", true);
                    stage++;
                }
                else if (Wait(900))
                {
                    Report("a request from the client starts the invasion", false);
                }
                break;
            case 8:
                if (Wait(120))
                {
                    InvasionLoader.Stop(false);
                }
                break;
            case 9:
                if (Wait(240))
                    End();
                break;
        }
    }

    static void Client(Player self)
    {
        switch (stage)
        {
            case 0:
                if (frame < 90) return;
                Report("a late joiner has the state at once", InvasionLoader.Active == Test && InvasionLoader.Progress == 3 && InvasionLoader.Wave == 0, $"active={InvasionLoader.Active?.Name} progress={InvasionLoader.Progress}");
                Report("the client's bar shows the state", Main.invasionProgress == 3 && Main.invasionProgressIcon == 3 && Main.invasionWaveMax == 2, $"bar={Main.invasionProgress}/{Main.invasionProgressMax} icon={Main.invasionProgressIcon}");
                stage++;
                break;
            case 1:
                if (InvasionLoader.Progress >= 5)
                {
                    Report("points reach the client in a packet", InvasionLoader.Progress == 5, $"progress={InvasionLoader.Progress}");
                    stage++;
                }
                else if (Wait(600))
                {
                    Report("points reach the client in a packet", false, $"progress={InvasionLoader.Progress}");
                }
                break;
            case 2:
                for (int k = 0; k < Main.maxNPCs; k++)
                {
                    NPC npc = Main.npc[k];
                    if (npc.active && npc.type == NPCID.BlueSlime)
                    {
                        npc.StrikeInstantKill();
                        Log.Info($"csinvasionmp client struck npc {k}");
                        stage++;
                        break;
                    }
                }
                if (stage == 2 && Wait(600))
                {
                    Report("the client sees the enemy", false);
                    stage = 3;
                }
                break;
            case 3:
                if (InvasionLoader.Wave == 1)
                {
                    Report("the wave change reaches the client", InvasionLoader.ProgressMax == 8, $"max={InvasionLoader.ProgressMax}");
                    stage++;
                }
                else if (Wait(900))
                {
                    Report("the wave change reaches the client", false, $"wave={InvasionLoader.Wave}");
                }
                break;
            case 4:
                if (InvasionLoader.Active == null)
                {
                    Report("the end reaches the client, with the downed flag", InvasionLoader.IsDowned(Test));
                    stage++;
                }
                else if (Wait(900))
                {
                    Report("the end reaches the client, with the downed flag", false);
                }
                break;
            case 5:
                if (Wait(60))
                    Report("Start from a client sends the request", InvasionLoader.Start(Test));
                break;
            case 6:
                if (InvasionLoader.Active == Test)
                {
                    Report("the server's answer reaches the client", true);
                    stage++;
                }
                else if (Wait(600))
                {
                    Report("the server's answer reaches the client", false);
                }
                break;
            case 7:
                if (InvasionLoader.Active == null || Wait(600))
                {
                    Report("the failure reaches the client", InvasionLoader.Active == null);
                    End();
                }
                break;
        }
    }
}
