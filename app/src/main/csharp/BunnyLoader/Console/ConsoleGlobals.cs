using System.ComponentModel;
using Terraria;

namespace BunnyLoader;

public static class ConsoleGlobals
{
    public static Player player => Main.LocalPlayer;

    public static void print(object? value)
    {
        string text = value as string ?? DevConsole.Format(value);
        Main.NewText(text, 255, 255, 255, false);
        DevConsole.Print('p', text);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly object NoValue = new();
}
