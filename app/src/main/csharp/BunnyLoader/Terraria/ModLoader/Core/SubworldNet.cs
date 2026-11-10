using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.IO;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class SubworldNet
{
    public const byte KindSubworld = 22;

    const byte OpRequest = 0, OpAnswer = 1, OpStatus = 2, OpResult = 3, OpTravel = 4, OpState = 5;
    internal const byte Pending = 0, Yes = 1, No = 2;
    internal const byte Go = 0, Declined = 1, Nobody = 2;
    const double VoteSeconds = 30, ResultSeconds = 2.5, HostDelaySeconds = 0.5, RetrySeconds = 3, ConnectSeconds = 30, GiveUpSeconds = 150;

    internal sealed class Vote
    {
        internal ModSubworld? Target;
        internal string Name = "";
        internal readonly List<int> Players = new();
        internal readonly Dictionary<int, byte> Answers = new();
        internal DateTime Deadline;
        internal DateTime? Closing;
        internal byte Outcome = byte.MaxValue;

        internal byte AnswerOf(int player) => Answers.TryGetValue(player, out byte answer) ? answer : Pending;

        internal int SecondsLeft => Math.Max(0, (int)Math.Ceiling((Deadline - DateTime.UtcNow).TotalSeconds));
    }

    enum Rejoin { None, Leaving, Waiting, Connecting }

    static readonly object gate = new();
    static readonly Queue<Action> mainThread = new();
    static Vote? vote;
    static ModSubworld? hostTarget;
    static bool hostTravelPending;
    static DateTime hostTravelAt;
    static Rejoin rejoin;
    static DateTime nextTry, connectStarted, giveUp;
    static string address = "", password = "";
    static int port;
    static bool reloadPlayer;
    static GameHandle? clientBuffer;
    static ModSubworld? rejoinTarget;

    internal static bool IsHost => (ModNet.NetMode & 2) != 0;

    internal static bool IsClient => ModNet.NetMode == 1;

    internal static Vote? Current
    {
        get { lock (gate) return vote; }
    }

    internal static bool Rejoining => rejoin != Rejoin.None;

    internal static bool HostTraveling => hostTravelPending;

    internal static string RejoinText
    {
        get
        {
            try
            {
                string? text = rejoinTarget?.LoadingText;
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch (Exception e) { ItemHooks.Report("Subworld loading text", e); }
            return Language.GetTextValue("BunnyLoader.LoadingDimension");
        }
    }

    internal static void Install() => On_Main.DoUpdate += DoUpdate;

    static string Text(string key, params object[] args) => Language.GetTextValue("BunnyLoader." + key, args);

    static ModSubworld? Find(string name) => name.Length == 0 ? null : SubworldLoader.Subworlds.FirstOrDefault(s => s.FullName == name);

    static string NameOf(ModSubworld? subworld)
    {
        if (subworld == null) return "";
        try { return subworld.DisplayName.Value ?? subworld.Name; }
        catch { return subworld.Name; }
    }

    internal static bool Refuse()
    {
        Main.NewText(Text("SubworldOnlyHost"), 255, 240, 20);
        return false;
    }

    internal static bool HostTravel(ModSubworld? target)
    {
        lock (gate)
        {
            if (vote != null || hostTravelPending) return false;
            var players = new List<int>();
            for (int i = 0; i < 255; i++)
                if (Main.player[i].active) players.Add(i);
            if (target == null || players.Count <= 1)
            {
                StartHostTravel(target);
                return true;
            }

            vote = new Vote { Target = target, Name = NameOf(target), Deadline = DateTime.UtcNow.AddSeconds(VoteSeconds) };
            vote.Players.AddRange(players);
            vote.Answers[Main.myPlayer] = Yes;
            Send(w =>
            {
                w.Write(OpRequest);
                w.Write(target.FullName);
                w.Write((float)VoteSeconds);
                w.Write((byte)players.Count);
                foreach (int p in players) w.Write((byte)p);
            }, -1);
            SendStatus();
        }

        SubworldVoteUI.Show();
        return true;
    }

    static void StartHostTravel(ModSubworld? target)
    {
        ModSubworld? owner = target ?? SubworldLoader.Current;
        hostTarget = target;
        hostTravelPending = true;
        hostTravelAt = DateTime.UtcNow.AddSeconds(HostDelaySeconds + SubworldTransition.LeaveSeconds(owner));
        Send(w =>
        {
            w.Write(OpTravel);
            w.Write(target?.FullName ?? "");
        }, -1);
        SubworldTransition.Leave(owner, null);
    }

    internal static void Answer(bool accept)
    {
        lock (gate)
        {
            if (vote == null || vote.Closing != null || vote.AnswerOf(Main.myPlayer) != Pending) return;
            vote.Answers[Main.myPlayer] = accept ? Yes : No;
        }

        Send(w =>
        {
            w.Write(OpAnswer);
            w.Write(accept);
        }, -1);
    }

    internal static string? Admit(int player)
    {
        if (!IsHost) return null;
        if (SubworldTravel.Busy || hostTravelPending) return Text("SubworldBusy");
        string current = SubworldLoader.Current?.FullName ?? "";
        Send(w =>
        {
            w.Write(OpState);
            w.Write(current);
        }, player);
        return null;
    }

    static void Send(Action<BinaryWriter> write, int to)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindSubworld);
        write(writer);
        writer.Flush();
        if (!ModNet.Send(stream.GetBuffer(), (int)stream.Length, to, -1)) Log.Warn("Subworlds: a message was not sent");
    }

    static void SendStatus()
    {
        Vote? v = vote;
        if (v == null) return;
        Send(w =>
        {
            w.Write(OpStatus);
            w.Write((byte)v.Players.Count);
            foreach (int p in v.Players)
            {
                w.Write((byte)p);
                w.Write(v.AnswerOf(p));
            }
        }, -1);
    }

    static void Close(byte outcome)
    {
        Vote? v = vote;
        if (v == null || v.Closing != null) return;
        v.Outcome = outcome;
        v.Closing = DateTime.UtcNow.AddSeconds(ResultSeconds);
        SendStatus();
        Send(w =>
        {
            w.Write(OpResult);
            w.Write(outcome);
        }, -1);
        if (outcome == Nobody) Main.NewText(Text("SubworldNobodyAccepted", v.Name), 255, 240, 20);
        if (outcome == Go)
        {
            v.Closing = DateTime.UtcNow;
            StartHostTravel(v.Target);
        }
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        byte op = reader.ReadByte();
        lock (gate)
        {
            if (IsHost)
            {
                if (op == OpAnswer) HostAnswer(from, reader.ReadBoolean());
                return;
            }

            switch (op)
            {
                case OpRequest:
                {
                    ModSubworld? target = Find(reader.ReadString());
                    float seconds = reader.ReadSingle();
                    var v = new Vote { Target = target, Name = NameOf(target), Deadline = DateTime.UtcNow.AddSeconds(seconds) };
                    int count = reader.ReadByte();
                    for (int i = 0; i < count; i++) v.Players.Add(reader.ReadByte());
                    vote = v;
                    Queue(SubworldVoteUI.Show);
                    break;
                }
                case OpStatus when vote != null:
                {
                    int count = reader.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int player = reader.ReadByte();
                        byte answer = reader.ReadByte();
                        if (answer != Pending || player != Main.myPlayer) vote.Answers[player] = answer;
                    }

                    break;
                }
                case OpResult when vote != null:
                    vote.Outcome = reader.ReadByte();
                    vote.Closing = DateTime.UtcNow.AddSeconds(vote.Outcome == Go ? ConnectSeconds : ResultSeconds);
                    break;
                case OpTravel:
                {
                    ModSubworld? target = Find(reader.ReadString());
                    vote = null;
                    Queue(() => SubworldTransition.Leave(target ?? SubworldLoader.Current, () => ClientLeave(target)));
                    break;
                }
                case OpState:
                {
                    ModSubworld? current = Find(reader.ReadString());
                    Queue(() => SubworldTravel.SetRemote(current));
                    break;
                }
            }
        }
    }

    static void HostAnswer(int from, bool accept)
    {
        Vote? v = vote;
        if (v == null || v.Closing != null || !v.Players.Contains(from) || v.AnswerOf(from) != Pending) return;
        v.Answers[from] = accept ? Yes : No;
        SendStatus();
    }

    static void Queue(Action action)
    {
        lock (mainThread) mainThread.Enqueue(action);
    }

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        try { Tick(gameTime); }
        catch (Exception e) { ItemHooks.Report("Subworld travel (multiplayer)", e); }
    }

    static void Tick(GameTime gameTime)
    {
        while (true)
        {
            Action? action;
            lock (mainThread) action = mainThread.Count > 0 ? mainThread.Dequeue() : null;
            if (action == null) break;
            action();
        }

        lock (gate)
        {
            if (IsHost) HostTick();
            if (vote?.Closing is { } closing && DateTime.UtcNow >= closing) vote = null;
        }

        SubworldVoteUI.Update(gameTime);
        SubworldTransition.Update();
        RejoinTick();
    }

    static void HostTick()
    {
        if (hostTravelPending && DateTime.UtcNow >= hostTravelAt)
        {
            hostTravelPending = false;
            if (!SubworldTravel.BeginLocal(hostTarget)) SubworldTransition.Stop();
        }

        Vote? v = vote;
        if (v == null || v.Closing != null) return;
        bool changed = v.Players.RemoveAll(p => p != Main.myPlayer && !Main.player[p].active) > 0;
        if (changed) SendStatus();
        var others = v.Players.Where(p => p != Main.myPlayer).ToList();
        if (others.Count == 0)
        {
            Close(Go);
            return;
        }

        if (others.Any(p => v.AnswerOf(p) == No))
        {
            Close(others.Any(p => v.AnswerOf(p) == Yes) ? Declined : Nobody);
            return;
        }

        if (others.All(p => v.AnswerOf(p) == Yes))
        {
            Close(Go);
            return;
        }

        if (DateTime.UtcNow < v.Deadline) return;
        foreach (int p in others)
            if (v.AnswerOf(p) == Pending) v.Answers[p] = No;
        Close(others.Any(p => v.AnswerOf(p) == Yes) ? Declined : Nobody);
    }

    static void ClientLeave(ModSubworld? target)
    {
        if (!IsClient || Main.gameMenu)
        {
            SubworldTransition.Stop();
            return;
        }

        address = Netplay.ServerIPText ?? "";
        port = Netplay.ListenPort;
        password = Netplay.ServerPassword ?? "";
        reloadPlayer = !SubworldWorldHooks.SavesPlayer;
        rejoinTarget = target ?? SubworldLoader.Current;
        giveUp = DateTime.UtcNow.AddSeconds(GiveUpSeconds);
        rejoin = Rejoin.Leaving;
        Main.menuMode = 10;
        WorldGen.SaveAndQuit();
    }

    internal static void AfterSaveAndQuit()
    {
        if (rejoin != Rejoin.Leaving) return;
        rejoin = Rejoin.Waiting;
        nextTry = DateTime.UtcNow.AddSeconds(RetrySeconds);
    }

    static void RejoinTick()
    {
        DateTime now = DateTime.UtcNow;
        switch (rejoin)
        {
            case Rejoin.Waiting when now >= nextTry && Main.gameMenu:
                if (now >= giveUp)
                {
                    rejoin = Rejoin.None;
                    SubworldTransition.Stop();
                    Log.Warn("Subworlds: the server did not take the player back in time");
                    return;
                }

                if (reloadPlayer)
                {
                    SubworldWorldHooks.ReloadPlayer();
                    reloadPlayer = false;
                }

                Connect();
                break;
            case Rejoin.Connecting when !Main.gameMenu && IsClient:
                rejoin = Rejoin.None;
                SubworldTravel.ShowTouchControlsPrompt(true);
                SubworldTransition.Arrive();
                break;
            case Rejoin.Connecting when Main.gameMenu && (Main.netMode == 0 || now - connectStarted > TimeSpan.FromSeconds(ConnectSeconds)):
                rejoin = Rejoin.Waiting;
                nextTry = now.AddSeconds(RetrySeconds);
                break;
        }
    }

    static void Connect()
    {
        PlayerFileData player = Main.ActivePlayerFileData;
        if (!player.IsNull) Main.SelectPlayer(player);
        var buffers = NetMessage.buffer;
        if (!buffers.IsNull && buffers.Length > 256 && buffers[256].IsNull)
        {
            MessageBuffer buffer = MessageBuffer.New();
            buffer.whoAmI = 256;
            clientBuffer?.Dispose();
            clientBuffer = new GameHandle(buffer.Pointer);
            buffers[256] = buffer;
        }

        Main.netMode = 1;
        Netplay.ListenPort = port;
        Netplay.ServerPassword = password;
        Main.getIP = address;
        if (!Netplay.SetRemoteIP(address))
        {
            rejoin = Rejoin.None;
            Log.Warn($"Subworlds: the server address '{address}' could not be used to come back");
            return;
        }

        MPSessionManager sessions = MPSessionManager.Instance;
        if (!sessions.IsNull)
            sessions.CurrentSession = sessions.CreateNewDirectSession(PlatformUser.CurrentUser, MPSessionManager.LobbyState.InProgress, address, port);
        SubworldTravel.ShowTouchControlsPrompt(false);
        Main.menuMode = 10;
        Netplay.StartTcpClient(false);
        connectStarted = DateTime.UtcNow;
        rejoin = Rejoin.Connecting;
    }
}
