using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CsNetPlayer;

public sealed class CsNetPlayerMod : Mod
{
    internal const byte Sync = 1;

    static int pass, fail, frames, client = -1, sawAt = -1;
    static bool reported, done;

    public override void Load() => On_Player.Update += Update;

    static CsNetPlayerMod Self => ModContent.GetInstance<CsNetPlayerMod>();

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        if (reader.ReadByte() != Sync) return;
        int who = reader.ReadByte();
        int value = reader.ReadInt32();
        bool fresh = reader.ReadBoolean();
        if (Main.netMode == 2 || Main.netMode == 3)
        {
            who = whoAmI;
            if (fresh) NetPlayer.NewPlayerSyncs++;
        }
        NetPlayer m = Main.player[who].GetModPlayer<NetPlayer>();
        m.Value = value;
        m.Received++;
        if (Main.netMode == 2 || Main.netMode == 3) m.SyncPlayer(-1, who, fresh);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu) return;
        frames++;
        if (Main.netMode == 1) Client(self);
        else if (Main.netMode == 3) Host(self);
    }

    static void Client(Player p)
    {
        NetPlayer m = p.GetModPlayer<NetPlayer>();
        if (frames == 60) m.Value = 42;
        if (frames == 400) m.Value = 77;
        if (frames != 900) return;
        Player host = Main.player[0];
        int seen = host.IsNull || host == p ? -1 : host.GetModPlayer<NetPlayer>().Value;
        Report("the client sees the host's ModPlayer", seen == 5, $"host value={seen}");
        Report("SendClientChanges sends the client's changes", m.Sends >= 2, $"sends={m.Sends}");
        done = true;
        Self.Logger.Info($"csnetplayer client END pass={pass} fail={fail}");
    }

    static void Host(Player p)
    {
        if (frames == 60) p.GetModPlayer<NetPlayer>().Value = 5;
        if (client < 0)
        {
            for (int k = 0; k < 255; k++)
            {
                Player other = Main.player[k];
                if (k != Main.myPlayer && !other.IsNull && other.active) client = k;
            }
            if (client < 0 && frames > 60 * 60 * 3)
            {
                Report("a client joined", false, "none in 3 minutes");
                Finish();
            }
            return;
        }
        NetPlayer remote = Main.player[client].GetModPlayer<NetPlayer>();
        if (!reported)
        {
            if (remote.Value != 77)
            {
                if (frames > 60 * 60 * 4)
                {
                    Report("the host sees the client's last value", false, $"value={remote.Value}");
                    reported = true;
                }
                return;
            }
            reported = true;
            sawAt = frames;
            Report("PlayerConnect runs for the client", NetPlayer.Connects.Contains(client), $"client={client}");
            Report("the client's SyncPlayer arrives as a new player", NetPlayer.NewPlayerSyncs >= 1, $"syncs={NetPlayer.NewPlayerSyncs}");
            Report("the host sees the client's changes", remote.Value == 77 && remote.Received >= 3, $"value={remote.Value} received={remote.Received}");
            Report("SyncPlayer runs on the host for the client", NetPlayer.HostSyncs > 0, $"calls={NetPlayer.HostSyncs}");
            Self.Logger.Info("csnetplayer waiting for the client to leave");
            return;
        }
        if (NetPlayer.Disconnects.Contains(client))
        {
            Report("PlayerDisconnect runs when the client leaves", true, $"client={client}");
            Finish();
        }
        else if (frames - sawAt > 60 * 60 * 2)
        {
            Report("PlayerDisconnect runs when the client leaves", false, "the client did not leave in 2 minutes");
            Finish();
        }
    }

    static void Finish()
    {
        done = true;
        Self.Logger.Info($"csnetplayer END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Self.Logger.Info($"csnetplayer {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
