using System;
using System.Linq;
using BunnyLoader.Interop;
using Terraria.Chat;
using Terraria.Chat.Commands;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class CommandHooks
{
    internal static void Install() => ItemHooks.Once("ModCommand", () =>
    {
        On_ChatCommandProcessor.ProcessIncomingMessage += Incoming;
        On_ChatHelper.SendChatMessageFromClient += FromClient;
        On_HelpCommand.ComposeMessage += Help;
    });

    static string? TextOf(ChatMessage message) => message.IsNull ? null : message.Text;

    static void Incoming(On_ChatCommandProcessor.orig_ProcessIncomingMessage orig, ChatCommandProcessor self, ChatMessage message, int clientId)
    {
        string? text = TextOf(message);
        if (Main.netMode != 1 && text != null && text.StartsWith('/') && Handle(text, clientId))
        {
            message.Consume();
            return;
        }

        orig(self, message, clientId);
    }

    static bool Handle(string text, int clientId)
    {
        try
        {
            if ((Main.netMode & 2) == 0)
                return CommandLoader.HandleCommand(text, new ChatCommandCaller());
            var player = new PlayerCommandCaller(clientId);
            if (Main.netMode == 2 || clientId != Main.myPlayer)
                return CommandLoader.HandleCommand(text, player);
            return CommandLoader.HandleCommand(text, new ChatCommandCaller()) || CommandLoader.HandleCommand(text, player);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModCommand", e);
            return false;
        }
    }

    static void FromClient(On_ChatHelper.orig_SendChatMessageFromClient orig, ChatMessage message)
    {
        string? text = TextOf(message);
        if (Main.netMode == 1 && text != null && text.StartsWith('/'))
        {
            try
            {
                if (CommandLoader.HandleCommand(text, new ChatCommandCaller()))
                    return;
            }
            catch (Exception e)
            {
                ItemHooks.Report("ModCommand", e);
            }
        }

        orig(message);
    }

    static NetworkText Help(On_HelpCommand.orig_ComposeMessage orig, GameObject aliases)
    {
        NetworkText text = orig(aliases);
        CommandType type = (Main.netMode & 2) != 0 ? CommandType.Chat | CommandType.Server : CommandType.Chat;
        string[] lines = CommandLoader.GetHelp(type).Select(h => "/" + h.Item1 + (h.Item2.Length > 0 ? " " + h.Item2 : "")).ToArray();
        return lines.Length == 0 ? text : NetworkText.FromLiteral(text.ToString() + "\n" + string.Join("\n", lines));
    }
}
