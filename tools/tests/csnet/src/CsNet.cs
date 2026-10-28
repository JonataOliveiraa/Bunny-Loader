using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CsNet;

public sealed class CsNetMod : Mod
{
    const byte Ping = 1, Pong = 2, Hello = 3;

    static int frames;
    static bool done;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu || ++frames != 120) return;
        done = true;
        ModContent.GetInstance<CsNetMod>().InWorld();
    }

    void InWorld()
    {
        if (Main.netMode == 0)
        {
            try
            {
                GetPacket().Send();
                Report("Send in single player throws", false, "no exception");
            }
            catch (InvalidOperationException)
            {
                Report("Send in single player throws", true, "");
            }
            Logger.Info("csnet END (single player; host and join for the network part)");
            return;
        }
        if (Main.netMode == 1)
        {
            ModPacket ping = GetPacket();
            ping.Write(Ping);
            ping.Write("hello from the client");
            ping.Write(12345);
            ping.Send();
            Logger.Info("csnet sent ping to the server");
            return;
        }
        ModPacket hello = GetPacket();
        hello.Write(Hello);
        hello.Write(Main.myPlayer);
        hello.Send();
        Logger.Info("csnet server broadcast hello");
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        byte kind = reader.ReadByte();
        switch (kind)
        {
            case Ping:
            {
                string text = reader.ReadString();
                int number = reader.ReadInt32();
                Report($"ping from {whoAmI}", text == "hello from the client" && number == 12345 && whoAmI >= 0 && whoAmI < 255, $"'{text}' {number}");
                ModPacket pong = GetPacket();
                pong.Write(Pong);
                pong.Write(number + 1);
                pong.Send(whoAmI);
                break;
            }
            case Pong:
            {
                int number = reader.ReadInt32();
                Report("pong", number == 12346 && whoAmI == 256, $"{number} from {whoAmI}");
                Logger.Info("csnet END (client)");
                break;
            }
            case Hello:
                Report("broadcast from the server", whoAmI == 256, $"from {whoAmI} player {reader.ReadInt32()}");
                break;
        }
    }

    void Report(string name, bool ok, string detail) => Logger.Info($"csnet {name}: {(ok ? "ok" : "FAIL")} {detail}");
}
