using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.Chat.Commands;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CsModCommand;

public sealed class Probe
{
    public static int Calls;
    public static string Input = "";
    public static string[] Args = Array.Empty<string>();
    public static CommandType CallerType;
    public static bool LocalPlayer;
}

public sealed class TestCommand : ModCommand
{
    public override string Command => "cstest";

    public override CommandType Type => CommandType.Chat;

    public override string Description => "Test command";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Probe.Calls++;
        Probe.Input = input;
        Probe.Args = args;
        Probe.CallerType = caller.CommandType;
        Probe.LocalPlayer = caller.Player.whoAmI == Main.myPlayer;
        caller.Reply("cstest ran");
    }
}

public sealed class ExactCommand : ModCommand
{
    public override string Command => "csexact";

    public override CommandType Type => CommandType.World;

    public override bool IsCaseSensitive => true;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Probe.Calls++;
        Probe.Args = args;
    }
}

public sealed class UsageCommand : ModCommand
{
    public override string Command => "csusage";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/csusage <number>";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Probe.Calls++;
        if (args.Length == 0)
            throw new UsageException("csusage needs a number");
        throw new UsageException();
    }
}

public sealed class ServerCommand : ModCommand
{
    public override string Command => "csserver";

    public override CommandType Type => CommandType.Server;

    public override void Action(CommandCaller caller, string input, string[] args) => Probe.Calls++;
}

public sealed class CsModCommandMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsModCommandMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done || ++frames < 10)
            return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csmodcommand END pass={pass} fail={fail}");
    }

    static void Send(string text) => ChatManager.Commands.ProcessIncomingMessage(ChatManager.Commands.CreateOutgoingMessage(text), Main.myPlayer);

    static void Run()
    {
        Send("/cstest Alpha BETA");
        Report("a Chat command runs from the chat with lowercased arguments", Probe.Calls == 1 && Probe.Args.SequenceEqual(new[] { "alpha", "beta" }) &&
            Probe.Input == "/cstest alpha beta" && Probe.CallerType == CommandType.Chat && Probe.LocalPlayer,
            $"calls={Probe.Calls} input='{Probe.Input}' args={string.Join(",", Probe.Args)} caller={Probe.CallerType}");
        Send("/CsTest");
        Report("the command name ignores case", Probe.Calls == 2 && Probe.Args.Length == 0, $"calls={Probe.Calls}");
        Send("/csexact Hello World");
        Report("a case-sensitive World command keeps the arguments and runs in single player", Probe.Calls == 3 && Probe.Args.SequenceEqual(new[] { "Hello", "World" }),
            $"calls={Probe.Calls} args={string.Join(",", Probe.Args)}");
        Send("/csusage");
        Send("/csusage 1");
        Report("UsageException with and without a message is answered without breaking the chat", Probe.Calls == 5, $"calls={Probe.Calls}");
        Send("/csserver");
        Report("a Server command does not run from the chat in single player", Probe.Calls == 5, $"calls={Probe.Calls}");
        Send("/CsModCommand:cstest one");
        Report("mod:command picks the mod", Probe.Calls == 6 && Probe.Args.SequenceEqual(new[] { "one" }), $"calls={Probe.Calls}");
        Send("/nosuchmod:cstest");
        Report("an unknown mod in mod:command does not run anything", Probe.Calls == 6, $"calls={Probe.Calls}");
        var help = CommandLoader.GetHelp(CommandType.Chat);
        Report("GetHelp lists the Chat and World commands only", help.Any(h => h.Item1 == "cstest" && h.Item2 == "Test command") && help.Any(h => h.Item1 == "csexact") &&
            help.All(h => h.Item1 != "csserver"), string.Join(" ", help.Select(h => h.Item1)));
        Report("Matches turns World into Chat in single player", CommandLoader.Matches(CommandType.World, CommandType.Chat) && !CommandLoader.Matches(CommandType.Server, CommandType.Chat), "");
        string text = HelpCommand.ComposeMessage(HelpCommand.GetCommandAliasesByID()).ToString();
        Report("/help adds the mod commands", text.Contains("/cstest Test command") && text.Contains("/csexact") && !text.Contains("/csserver"), text.Replace('\n', '|'));
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"csmodcommand {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
