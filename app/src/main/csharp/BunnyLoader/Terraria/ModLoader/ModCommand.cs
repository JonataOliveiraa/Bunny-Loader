using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Chat;
using Terraria.Localization;

namespace Terraria.ModLoader;

[Flags]
public enum CommandType
{
    Chat = 1,
    Server = 2,
    Console = 4,
    World = 8
}

public interface CommandCaller
{
    CommandType CommandType { get; }

    Player Player { get; }

    void Reply(string text, Color color = default);
}

public abstract class ModCommand : ModType
{
    public abstract string Command { get; }

    public abstract CommandType Type { get; }

    public virtual string Usage => "/" + Command;

    public virtual string Description => "";

    public virtual bool IsCaseSensitive => false;

    protected sealed override void Register() => CommandLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public abstract void Action(CommandCaller caller, string input, string[] args);
}

public class UsageException : Exception
{
    internal string? msg;

    internal Color color = new(255, 0, 0, 255);

    public UsageException()
    {
    }

    public UsageException(string msg)
    {
        this.msg = msg;
    }

    public UsageException(string msg, Color color)
    {
        this.msg = msg;
        this.color = color;
    }
}

internal sealed class ChatCommandCaller : CommandCaller
{
    public CommandType CommandType => CommandType.Chat;

    public Player Player => Main.player[Main.myPlayer];

    public void Reply(string text, Color color = default)
    {
        if (color.R == 0 && color.G == 0 && color.B == 0 && color.A == 0)
            color = new Color(255, 255, 255, 255);
        foreach (string line in text.Split('\n'))
            Main.NewText(line, color.R, color.G, color.B, false);
    }
}

internal sealed class PlayerCommandCaller : CommandCaller
{
    readonly int player;

    public PlayerCommandCaller(int player)
    {
        this.player = player;
    }

    public CommandType CommandType => CommandType.Server;

    public Player Player => Main.player[player];

    public void Reply(string text, Color color = default)
    {
        if (color.R == 0 && color.G == 0 && color.B == 0 && color.A == 0)
            color = new Color(255, 255, 255, 255);
        foreach (string line in text.Split('\n'))
            ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(line), color, player);
    }
}
