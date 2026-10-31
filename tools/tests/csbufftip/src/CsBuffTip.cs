using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CsBuffTip;

public sealed class CsBuffTipMod : Mod
{
}

public class TipGlobalBuff : GlobalBuff
{
    public static int SizeCalls, DrawCalls, OriginX = int.MinValue, OriginY = int.MinValue;

    public override void CustomBuffTipSize(string buffTip, List<Vector2> sizes)
    {
        SizeCalls++;
        if (buffTip == "csbufftip") sizes.Add(new Vector2(5000f, 50f));
    }

    public override void DrawCustomBuffTip(string buffTip, SpriteBatch spriteBatch, int originX, int originY)
    {
        DrawCalls++;
        OriginX = originX;
        OriginY = originY;
        spriteBatch.DrawString(FontAssets.MouseText.Value, "extra", new Vector2(originX, originY), Color.Yellow);
    }
}

public class TipPlayer : ModPlayer
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBuffTipMod>().Logger;

    public override void OnEnterWorld()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public override void DrawPlayer(Camera camera)
    {
        if (done || Player.whoAmI != Main.myPlayer || ++frames != 120) return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csbufftip END pass={pass} fail={fail}");
    }

    static void Run()
    {
        SpriteFont font = FontAssets.MouseText.Value;
        Vector2 measured = font.MeasureString("csbufftip");
        Report("FontAssets.MouseText measures text", measured.X > 0 && measured.Y > 0, $"size={measured}");

        int x = 100, y = 100;
        Main.instance.MouseText_DrawBuffTooltip("Name", "csbufftip", ref x, ref y);
        int expected = (int)(Main.screenWidth - 5000f - 24f);
        Report("CustomBuffTipSize pushes the tooltip back on screen", TipGlobalBuff.SizeCalls == 1 && x <= expected,
            $"calls={TipGlobalBuff.SizeCalls} x={x} expected<={expected} screen={Main.screenWidth}");
        Report("DrawCustomBuffTip draws under the buff text", TipGlobalBuff.DrawCalls == 1 && TipGlobalBuff.OriginX == x && TipGlobalBuff.OriginY == y + (int)measured.Y,
            $"calls={TipGlobalBuff.DrawCalls} origin={TipGlobalBuff.OriginX},{TipGlobalBuff.OriginY} xy={x},{y}");

        int x2 = 100, y2 = 100;
        Main.instance.MouseText_DrawBuffTooltip("Name", "plain", ref x2, ref y2);
        Report("a tooltip without extra sizes keeps its place", x2 == 100 && TipGlobalBuff.SizeCalls == 2, $"x={x2} calls={TipGlobalBuff.SizeCalls}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbufftip {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
