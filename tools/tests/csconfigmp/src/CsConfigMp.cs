using System;
using System.ComponentModel;
using System.IO;
using log4net;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CsConfigMp;

public sealed class SharedConfig : ModConfig
{
    public static SharedConfig Instance = null!;
    public static int Changed, Replies, Rejections, AskedBy = -1;
    public static bool LastSuccess;
    public static int LastRequestor = -2;
    public static string LastMessage = "";

    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Range(0, 100)]
    [DefaultValue(10)]
    public int Number;

    [ReloadRequired]
    public bool Restart;

    public override void OnChanged() => Changed++;

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        AskedBy = whoAmI;
        if (((SharedConfig)pendingConfig).Number == 99)
        {
            Rejections++;
            message = NetworkText.FromKey("tModLoader.ModConfigServerResponse", "no 99");
            return false;
        }

        return true;
    }

    public override void HandleAcceptClientChangesReply(bool success, int player, NetworkText message)
    {
        Replies++;
        LastSuccess = success;
        LastRequestor = player;
        LastMessage = message.ToString();
    }
}

public sealed class LocalConfig : ModConfig
{
    public static LocalConfig Instance = null!;

    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(1)]
    public int Value;
}

public sealed class CsConfigMpMod : Mod
{
    const int Timeout = 3000;

    static int frames, pass, fail, step, since, replies;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsConfigMpMod>().Logger;

    static SharedConfig Shared => SharedConfig.Instance;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = step = since = 0;
            return;
        }

        if (i != Main.myPlayer || done || Main.netMode == 0)
            return;
        frames++;
        try
        {
            if (Main.netMode == 1)
                Client();
            else
                Host();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done)
            Log.Info($"csconfigmp END pass={pass} fail={fail}");
    }

    static string FileOf(ModConfig config) => Path.Combine(ConfigManager.ModConfigPath, $"{config.Mod.Name}_{config.Name}.json");

    static string Read(ModConfig config) => File.Exists(FileOf(config)) ? File.ReadAllText(FileOf(config)) : "";

    static bool Waited(bool condition, string name, string detail)
    {
        if (condition)
            return true;
        if (frames - since < Timeout)
            return false;
        Report(name, false, "timed out: " + detail);
        done = true;
        return false;
    }

    static void Next()
    {
        step++;
        since = frames;
    }

    static void Host()
    {
        switch (step)
        {
            case 0:
                if (frames < 5)
                    return;
                var pending = (SharedConfig)Shared.Clone();
                pending.Number = 42;
                pending.Restart = false;
                ConfigSaveResult result = Shared.SaveChanges(pending);
                Report("the host saves a server-side config directly", result == ConfigSaveResult.Success && Shared.Number == 42 && Read(Shared).Contains("\"Number\": 42"), $"result={result} number={Shared.Number}");
                Next();
                break;
            case 1:
                if (!Waited(Shared.Number == 55, "client request reaches the host", $"number={Shared.Number}"))
                    return;
                Report("a client request goes through AcceptClientChanges and is saved on the host", SharedConfig.AskedBy >= 0 && SharedConfig.AskedBy != Main.myPlayer && Read(Shared).Contains("\"Number\": 55") &&
                    SharedConfig.LastSuccess && SharedConfig.LastRequestor == SharedConfig.AskedBy,
                    $"asked={SharedConfig.AskedBy} replies={SharedConfig.Replies} requestor={SharedConfig.LastRequestor}");
                Next();
                break;
            case 2:
                if (!Waited(SharedConfig.Rejections >= 1, "rejected request", $"rejections={SharedConfig.Rejections}"))
                    return;
                Report("a request refused by AcceptClientChanges changes nothing on the host", Shared.Number == 55 && Read(Shared).Contains("\"Number\": 55"), $"number={Shared.Number}");
                Next();
                break;
            case 3:
                if (frames - since < 180)
                    return;
                var change = (SharedConfig)Shared.Clone();
                change.Number = 77;
                Report("the host changes the config while the client is in", Shared.SaveChanges(change) == ConfigSaveResult.Success && Shared.Number == 77, $"number={Shared.Number}");
                Next();
                break;
            case 4:
                if (!Waited(Shared.Number == 60, "final client request", $"number={Shared.Number}"))
                    return;
                Next();
                break;
            case 5:
                if (frames - since < 600)
                    return;
                var reset = (SharedConfig)Shared.Clone();
                reset.Number = 10;
                Shared.SaveChanges(reset, broadcast: false);
                done = true;
                break;
        }
    }

    static void Client()
    {
        switch (step)
        {
            case 0:
                if (frames < 30)
                    return;
                Report("the server values arrive on join without touching the local file", Shared.Number == 42 && SharedConfig.Changed >= 1 && !Read(Shared).Contains("\"Number\": 42"),
                    $"number={Shared.Number} changed={SharedConfig.Changed}");
                var pending = (SharedConfig)Shared.Clone();
                pending.Number = 55;
                ConfigSaveResult result = Shared.SaveChanges(pending);
                Report("a client change is sent to the server", result == ConfigSaveResult.RequestSentToServer && Shared.Number == 42, $"result={result} number={Shared.Number}");
                replies = SharedConfig.Replies;
                Next();
                break;
            case 1:
                if (!Waited(SharedConfig.Replies > replies, "accepted reply", $"replies={SharedConfig.Replies}"))
                    return;
                Report("the accepted change comes back with HandleAcceptClientChangesReply", SharedConfig.LastSuccess && SharedConfig.LastRequestor == Main.myPlayer && Shared.Number == 55 &&
                    SharedConfig.LastMessage == Language.GetTextValue("tModLoader.ModConfigAccepted") && !Read(Shared).Contains("\"Number\": 55"),
                    $"success={SharedConfig.LastSuccess} requestor={SharedConfig.LastRequestor} number={Shared.Number} message='{SharedConfig.LastMessage}'");
                Shared.Number = 99;
                ConfigSaveResult active = Shared.SaveChanges();
                Report("a change made on the active config is undone until the server answers", active == ConfigSaveResult.RequestSentToServer && Shared.Number == 55, $"result={active} number={Shared.Number}");
                replies = SharedConfig.Replies;
                Next();
                break;
            case 2:
                if (!Waited(SharedConfig.Replies > replies, "rejected reply", $"replies={SharedConfig.Replies}"))
                    return;
                string expected = Language.GetTextValue("tModLoader.ModConfigServerResponse", "no 99");
                Report("a refusal reaches the client with the server's message", !SharedConfig.LastSuccess && Shared.Number == 55 && SharedConfig.LastMessage == expected,
                    $"success={SharedConfig.LastSuccess} number={Shared.Number} message='{SharedConfig.LastMessage}' expected='{expected}'");
                var restart = (SharedConfig)Shared.Clone();
                restart.Restart = !Shared.Restart;
                Shared.SaveChanges(restart);
                replies = SharedConfig.Replies;
                Next();
                break;
            case 3:
                if (!Waited(SharedConfig.Replies > replies, "reload reply", $"replies={SharedConfig.Replies}"))
                    return;
                Report("a ReloadRequired change is refused by the server", !SharedConfig.LastSuccess && SharedConfig.LastMessage == Language.GetTextValue("tModLoader.ModConfigCantSaveBecauseChangesWouldRequireAReload"),
                    $"success={SharedConfig.LastSuccess} message='{SharedConfig.LastMessage}'");
                var local = (LocalConfig)LocalConfig.Instance.Clone();
                local.Value = 5;
                ConfigSaveResult localResult = LocalConfig.Instance.SaveChanges(local);
                Report("a client-side config is saved locally", localResult == ConfigSaveResult.Success && LocalConfig.Instance.Value == 5 && Read(LocalConfig.Instance).Contains("\"Value\": 5"), $"result={localResult}");
                var back = (LocalConfig)LocalConfig.Instance.Clone();
                back.Value = 1;
                LocalConfig.Instance.SaveChanges(back);
                Next();
                break;
            case 4:
                if (!Waited(Shared.Number == 77, "host change", $"number={Shared.Number}"))
                    return;
                Report("a change made by the host reaches the client", SharedConfig.LastSuccess && SharedConfig.LastRequestor != Main.myPlayer, $"requestor={SharedConfig.LastRequestor}");
                var last = (SharedConfig)Shared.Clone();
                last.Number = 60;
                Shared.SaveChanges(last);
                Next();
                break;
            case 5:
                if (!Waited(Shared.Number == 60, "final reply", $"number={Shared.Number}"))
                    return;
                done = true;
                break;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"csconfigmp {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
