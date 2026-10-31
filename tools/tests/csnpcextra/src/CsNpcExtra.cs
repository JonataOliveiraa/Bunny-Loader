using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsNpcExtra;

public sealed class CsNpcExtraMod : Mod
{
    public static readonly List<(string Text, Color Color)> Texts = new();

    public CsNpcExtraMod()
    {
        On_Main.NewText_string_Color_bool += NewText;
        On_Main.NewText_string_byte_byte_byte_bool += NewTextBytes;
        On_ChatHelper.BroadcastChatMessage += Broadcast;
    }

    static void Broadcast(On_ChatHelper.orig_BroadcastChatMessage orig, NetworkText text, Color color, int excludedPlayer)
    {
        Texts.Add((text.IsNull ? "" : text.ToString() ?? "", color));
        orig(text, color, excludedPlayer);
    }

    public override void Load() => On_Player.Update += Update;

    static void NewText(On_Main.orig_NewText_string_Color_bool orig, string? text, Color color, bool onlyCurrentPlayer)
    {
        Texts.Add((text ?? "", color));
        orig(text, color, onlyCurrentPlayer);
    }

    static void NewTextBytes(On_Main.orig_NewText_string_byte_byte_byte_bool orig, string? text, byte r, byte g, byte b, bool onlyCurrentPlayer)
    {
        Texts.Add((text ?? "", new Color(r, g, b)));
        orig(text, r, g, b, onlyCurrentPlayer);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            Checks.Reset();
            return;
        }
        if (i != Main.myPlayer) return;
        Checks.Frame(self);
    }
}
