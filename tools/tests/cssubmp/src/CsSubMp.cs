using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CsSubMp;

public sealed class MpDimension : ModSubworld
{
    public override int Width => 1000;

    public override int Height => 500;

    public override bool ShowLoadingScreen => false;

    public override float TransitionOutSeconds => 1.5f;

    public override float TransitionInSeconds => 1.5f;

    public static int CoveredDraws;

    public override void DrawTransition(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float progress)
    {
        if (Main.gameMenu) CoveredDraws++;
        base.DrawTransition(spriteBatch, progress);
    }

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Floor", (progress, configuration) =>
        {
            int floor = (int)Main.worldSurface + 1;
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = floor; y < floor + 20; y++)
                    WorldGen.PlaceTile(x, y, TileID.Stone, true, true);
        }, 1),
    };
}

public sealed class CsSubMpMod : Mod
{
    const string Player = "djdjd";

    static int frames, round, pass, fail, votesSeen, joinTries, waiting, afterAnswer, diagnose;
    static nint seen;
    static bool done, answered, joined;
    static string lastChat = "";
    static DateTime nextJoin, titleSince;
    static BunnyLoader.Interop.GameHandle? keep;

    static ILog Log => ModContent.GetInstance<CsSubMpMod>().Logger;

    static readonly Type net = typeof(ModSubworld).Assembly.GetType("Terraria.ModLoader.Core.SubworldNet", true)!;

    static bool Host => (Main.netMode & 2) != 0;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DoUpdate += DoUpdate;
        On_Main.NewText_string_byte_byte_byte_bool += NewText;
    }

    static void NewText(On_Main.orig_NewText_string_byte_byte_byte_bool orig, string? text, byte r, byte g, byte b, bool only)
    {
        lastChat = text ?? "";
        orig(text, r, g, b, only);
    }

    static object? Vote => net.GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

    static object? Field(object vote, string name) => vote.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vote);

    static void Answer(bool accept) => net.GetMethod("Answer", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { accept });

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (diagnose > 0 && --diagnose == 0)
        {
            var buffers = NetMessage.buffer;
            bool noBuffer = buffers.IsNull || buffers.Length <= 256 || buffers[256].IsNull;
            Log.Info($"cssubmp diagnose connection={(Netplay.Connection.IsNull ? "null" : "set")} buffers={(buffers.IsNull ? -1 : buffers.Length)} buffer256={(noBuffer ? "null" : "set")} netMode={Main.netMode} menu={Main.menuMode} disconnect={Netplay.Disconnect}");
        }

        if (joined || Main.netMode != 0 || !Main.gameMenu || Main.menuMode != 0 || DateTime.UtcNow < nextJoin) return;
        if (Environment.GetEnvironmentVariable("CSSUBMP_HOST") == "1" || File.Exists(Path.Combine(Main.PlayerPath ?? "", "cssubmp.host"))) return;
        if (!File.Exists(Path.Combine(Main.PlayerPath ?? "", "cssubmp.client"))) return;
        if (titleSince == default) titleSince = DateTime.UtcNow;
        if (DateTime.UtcNow - titleSince < TimeSpan.FromSeconds(25)) return;
        nextJoin = DateTime.UtcNow.AddSeconds(5);
        if (++joinTries > 6) return;
        try
        {
            Main.LoadPlayers(false);
            var list = new BunnyLoader.Interop.GameList(Main.PlayerList);
            PlayerFileData data = default;
            for (int i = 0; i < list.Count; i++)
            {
                var candidate = new PlayerFileData(list.Get<IntPtr>(i));
                if (Path.GetFileNameWithoutExtension(candidate.Path ?? "") == Player) data = candidate;
            }

            if (data.IsNull) return;
            Main.SelectPlayer(data);
            var buffers = NetMessage.buffer;
            bool missing = buffers[256].IsNull;
            if (missing)
            {
                MessageBuffer created = MessageBuffer.New();
                created.whoAmI = 256;
                keep = new BunnyLoader.Interop.GameHandle(created.Pointer);
                buffers[256] = created;
            }

            Log.Info($"cssubmp buffer 256 was {(missing ? "missing, created" : "there")}");
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
            Log.Info($"cssubmp joining 127.0.0.1:7777 as {Player} (try {joinTries}) session={(sessions.IsNull ? "no manager" : sessions.CurrentSession.IsNull ? "null" : "set")} local={Main.LocalModeServer} myPlayer={Main.myPlayer}");
            diagnose = 120;
        }
        catch (Exception e) { Log.Info("cssubmp join failed " + e); }
    }

    static void Update(On_Player.orig_Update orig, Terraria.Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done || Main.netMode == 0) return;
        joined = true;
        if (Main.ActiveWorldFileData.Pointer != seen || (!Host && Main.worldName != lastWorld))
        {
            seen = Main.ActiveWorldFileData.Pointer;
            lastWorld = Main.worldName ?? "";
            frames = 0;
        }

        try
        {
            frames++;
            if (Host) HostStep();
            else ClientStep();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done) Log.Info($"cssubmp END pass={pass} fail={fail}");
    }

    static string lastWorld = "";
    static object? lastVote;

    static int Players()
    {
        int count = 0;
        for (int i = 0; i < 255; i++)
            if (Main.player[i].active) count++;
        return count;
    }

    static void HostStep()
    {
        int players = Players();
        switch (round)
        {
            case 0 when players >= 2 && frames > 120:
                if (++waiting < 120) return;
                waiting = 0;
                Report("host: Enter starts a vote with a client in the world", SubworldLoader.Enter<MpDimension>() && Vote != null && !SubworldLoader.Traveling, "");
                round = 1;
                break;
            case 1 when SubworldLoader.IsActive<MpDimension>() && players >= 2 && frames > 60:
                Report("host: the accepted trip takes the host and the client back in", true, $"players={players} size={Main.maxTilesX}x{Main.maxTilesY}");
                Report("host: the world change stayed covered by the transition", MpDimension.CoveredDraws > 0, $"covered={MpDimension.CoveredDraws}");
                Log.Info("cssubmp SHOT hostsub");
                round = 2;
                waiting = 0;
                break;
            case 2:
                if (++waiting < 180) return;
                waiting = 0;
                Report("host: Exit needs no vote", SubworldLoader.Exit() && Vote == null, "");
                round = 3;
                break;
            case 3 when !SubworldLoader.InSubworld && players >= 2 && frames > 60:
                Report("host: back in the main world with the client", true, $"players={players}");
                round = 4;
                waiting = 0;
                break;
            case 4:
                if (++waiting < 180) return;
                waiting = 0;
                SubworldLoader.Enter<MpDimension>();
                round = 5;
                break;
            case 5 when Vote == null:
                if (++waiting < 60) return;
                waiting = 0;
                Report("host: a decline keeps everyone in the main world and the host hears nobody accepted", !SubworldLoader.InSubworld && lastChat.Contains("Mp Dimension"), $"chat={lastChat}");
                lastChat = "";
                SubworldLoader.Enter<MpDimension>();
                round = 6;
                break;
            case 6 when Vote == null:
                if (++waiting < 60) return;
                Report("host: no answer in 30 s cancels and the host hears nobody accepted", !SubworldLoader.InSubworld && lastChat.Contains("Mp Dimension"), $"chat={lastChat}");
                done = true;
                break;
        }
    }

    static void ClientStep()
    {
        object? vote = Vote;
        if (vote != null && !ReferenceEquals(vote, lastVote))
        {
            lastVote = vote;
            votesSeen++;
            waiting = 0;
            answered = false;
            var players = (IList)Field(vote, "Players")!;
            Report($"client: vote {votesSeen} shows every player", players.Count >= 2 && players.Contains(Main.myPlayer), $"players={players.Count}");
            Log.Info($"cssubmp SHOT vote{votesSeen}");
        }

        if (vote != null && !answered && Field(vote, "Closing") == null)
        {
            var answers = (IDictionary)Field(vote, "Answers")!;
            if (votesSeen == 1 && answers.Contains(Main.myPlayer) && Convert.ToInt32(answers[Main.myPlayer]) == 1)
            {
                answered = true;
                Report("client: the Accept button answers the vote", true, "");
                return;
            }

            if (++waiting < (votesSeen == 1 ? 600 : 240)) return;
            answered = true;
            if (votesSeen == 1)
            {
                Report("client: the Accept button answers the vote", false, "no tap arrived");
                Answer(true);
            }
            else if (votesSeen == 2) Answer(false);
            afterAnswer = 20;
            return;
        }

        if (afterAnswer > 0 && --afterAnswer == 0 && vote != null) Log.Info($"cssubmp SHOT answered{votesSeen}");
        if (vote == null)
        {
            if (votesSeen == 3 && !Host && lastChat.Contains("Mp Dimension")) Report("client: only the host hears that nobody accepted", false, $"chat={lastChat}");
        }
        if (votesSeen == 1 && SubworldLoader.IsActive<MpDimension>() && frames == 120)
        {
            Report("client: back in the server inside the dimension", Main.maxTilesX == 1000 && Main.maxTilesY == 500, $"size={Main.maxTilesX}x{Main.maxTilesY} world={Main.worldName}");
            Report("client: the reconnection stayed covered by the transition", MpDimension.CoveredDraws > 0, $"covered={MpDimension.CoveredDraws}");
            Report("client: Enter is refused for a client", !SubworldLoader.Enter<MpDimension>(), "");
            Log.Info("cssubmp SHOT clientsub");
        }

        if (votesSeen == 1 && !SubworldLoader.InSubworld && frames == 120 && Main.maxTilesX != 1000)
            Report("client: back in the main world after the host's Exit", true, $"size={Main.maxTilesX}x{Main.maxTilesY}");

        if (votesSeen == 3 && vote == null && frames > 60)
        {
            if (++waiting < 120) return;
            Report("client: still in the main world after the votes", !SubworldLoader.InSubworld, "");
            done = true;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssubmp {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
