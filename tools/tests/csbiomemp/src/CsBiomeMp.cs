using System;
using System.IO;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsBiomeMp;

public sealed class TestBiome : ModBiome
{
    internal static bool Force;

    public override bool IsBiomeActive(Player player) => Force;
}

public sealed class TestSystem : ModSystem
{
    internal static int Value, Received = -1, PostWorld, PostEverything, HijackSends, HijackGets;

    public override void NetSend(BinaryWriter writer) => writer.Write(Value);

    public override void NetReceive(BinaryReader reader) => Received = reader.ReadInt32();

    public override void PostUpdateWorld() => PostWorld++;

    public override void PostUpdateEverything() => PostEverything++;

    public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
    {
        HijackSends++;
        return false;
    }

    public override bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)
    {
        HijackGets++;
        return false;
    }
}

public sealed class CsBiomeMpMod : Mod
{
    const int On = 120, CheckOn = 720, Off = 780, CheckOff = 1380;

    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBiomeMpMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done || Other() < 0) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csbiomemp END pass={pass} fail={fail}");
    }

    static bool Client => Main.netMode == NetmodeID.MultiplayerClient;

    static int Other()
    {
        for (int k = 0; k < Main.maxPlayers; k++)
            if (k != Main.myPlayer && Main.player[k].active) return k;
        return -1;
    }

    static void Step(int f)
    {
        Player other = Main.player[Other()];
        switch (f)
        {
            case On:
                TestBiome.Force = true;
                if (!Client)
                {
                    TestSystem.Value = 1234;
                    NetMessage.SendData(MessageID.WorldData);
                }
                break;
            case CheckOn:
                Report("my own mod biome flag is on", Main.LocalPlayer.InModBiome<TestBiome>(), $"me={Main.LocalPlayer.whoAmI}");
                Report("the other device's mod biome flag reaches me", other.InModBiome<TestBiome>(), $"other={other.whoAmI}");
                if (Client)
                {
                    Report("ModSystem.NetReceive gets the host's world data", TestSystem.Received == 1234, $"received={TestSystem.Received}");
                    Report("PostUpdateWorld does not run on a client, as in tModLoader", TestSystem.PostWorld == 0, $"calls={TestSystem.PostWorld}");
                }
                else
                {
                    Report("PostUpdateWorld runs on the host", TestSystem.PostWorld > 0, $"calls={TestSystem.PostWorld}");
                }
                Report("PostUpdateEverything runs every frame", TestSystem.PostEverything >= CheckOn / 2, $"calls={TestSystem.PostEverything}");
                Report("HijackSendData and HijackGetData see the game's messages", TestSystem.HijackSends > 0 && TestSystem.HijackGets > 0, $"sends={TestSystem.HijackSends} gets={TestSystem.HijackGets}");
                break;
            case Off:
                TestBiome.Force = false;
                break;
            case CheckOff:
                Report("turning the biome off reaches the other device", !other.InModBiome<TestBiome>() && !Main.LocalPlayer.InModBiome<TestBiome>(), $"other={other.InModBiome<TestBiome>()} me={Main.LocalPlayer.InModBiome<TestBiome>()}");
                done = true;
                break;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbiomemp {(Client ? "client" : "host")} {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
