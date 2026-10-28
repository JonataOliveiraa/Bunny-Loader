using System.ComponentModel;
using Terraria;

namespace BunnyLoader;

/// <summary>
/// O que o console C# do Editor ja traz aberto (com using static), como os
/// atalhos do console JS: player e print.
/// </summary>
public static class ConsoleGlobals
{
    /// <summary>O jogador local (Main.LocalPlayer).</summary>
    public static Player player => Main.LocalPlayer;

    /// <summary>Uma linha no chat do jogo (e na saida do Editor, como o print do JS).</summary>
    public static void print(object? value)
    {
        string text = value as string ?? DevConsole.Format(value);
        Main.NewText(text, 255, 255, 255, false);
        DevConsole.Print('p', text);
    }

    /// <summary>O resultado de um trecho sem valor (comandos, nao uma expressao).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly object NoValue = new();
}
