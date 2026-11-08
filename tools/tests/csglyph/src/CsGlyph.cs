using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGlyph;

public sealed class CsGlyphMod : Mod
{
    static int frame, pass, fail, step, slot, shots, waited;
    static Item kept;
    static bool show;

    static ILog Log => ModContent.GetInstance<CsGlyphMod>().Logger;

    internal static int DualUse => ModContent.Find<ModItem>("ExampleMod/ExampleDualUseWeapon").Type;

    internal static bool Showing => show;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csglyph {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer) return;
        frame++;
        try
        {
            switch (step)
            {
                case 0 when frame == 30:
                    slot = self.selectedItem;
                    kept = self.inventory[slot].Clone();
                    self.inventory[slot] = new Item(DualUse);
                    show = true;
                    Terraria.UI.ItemTooltip tooltip = Lang.GetTooltip(DualUse);
                    string lines = string.Join("|", Enumerable.Range(0, tooltip.Lines).Select(tooltip.GetLine));
                    Report("the dual use weapon's tooltip has the Interact glyph", lines.Contains($"[ct:1,{DualUse}]"), lines);
                    step = 1;
                    break;
                case 1 when frame == 90:
                    Log.Info($"csglyph SHOT {++shots} the mod glyph should be the swap button");
                    step = 2;
                    break;
                case 2 when frame == 600:
                    show = false;
                    Log.Info($"csglyph FTAP {++shots} 0.019 0.034");
                    waited = frame;
                    step = 3;
                    break;
                case 3 when Main.playerInventory && frame > waited + 120 || frame == waited + 900:
                    Report("tapping the chest opens the inventory", Main.playerInventory);
                    Log.Info($"csglyph FTAP {++shots} 0.059 0.322");
                    waited = frame;
                    step = 4;
                    break;
                case 4 when frame == waited + 240:
                    Log.Info($"csglyph SHOT {++shots} the tooltip glyph of the dual use weapon");
                    step = 5;
                    break;
                case 5 when frame == waited + 1200:
                    self.inventory[slot] = kept;
                    show = false;
                    step = 99;
                    Log.Info($"csglyph END pass={pass} fail={fail}");
                    break;
            }
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            step = 99;
            Log.Info($"csglyph END pass={pass} fail={fail}");
        }
    }
}

public sealed class CsGlyphSystem : ModSystem
{
    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!CsGlyphMod.Showing) return;
        Utils.DrawBorderString(spriteBatch, $"Mod [ct:1,{CsGlyphMod.DualUse}]", new Vector2(300, 260), Color.White, 1.4f);
        Utils.DrawBorderString(spriteBatch, $"Sword [ct:1,{ItemID.CopperShortsword}]", new Vector2(300, 320), Color.White, 1.4f);
        Utils.DrawBorderString(spriteBatch, $"Portal [ct:1,{ItemID.PortalGun}]", new Vector2(300, 380), Color.White, 1.4f);
        Utils.DrawBorderString(spriteBatch, $"Small [ct:1,{CsGlyphMod.DualUse}]", new Vector2(300, 440), Color.White, 1f);
        Utils.DrawBorderString(spriteBatch, $"SmallPortal [ct:1,{ItemID.PortalGun}]", new Vector2(300, 480), Color.White, 1f);
    }
}
