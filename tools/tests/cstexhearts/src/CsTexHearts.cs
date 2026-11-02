using System;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsTexHearts;

public sealed class CsTexHeartsMod : Mod
{
    const int End = 700;
    static int frame;

    static ILog Log => ModContent.GetInstance<CsTexHeartsMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DoDraw += DoDraw;
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        if (!Main.gameMenu && frame >= 60 && frame < 650)
        {
            Player me = Main.player[Main.myPlayer];
            me.statLife = me.statLifeMax2 * 47 / 100;
            me.statMana = me.statManaMax2 * 35 / 100;
        }
        orig(self, gameTime);
    }

    static void Give(Player p)
    {
        if (!ModContent.TryFind("ExampleMod/ExampleStatIncreasePlayer", out ModPlayer template))
        {
            Log.Info("cstexhearts no Example Mod: vanilla hearts only");
            return;
        }
        ModPlayer mp = p.GetModPlayer(template);
        Type t = mp.GetType();
        t.GetField("exampleLifeFruits", BindingFlags.Public | BindingFlags.Instance)?.SetValue(mp, 3);
        t.GetField("exampleManaCrystals", BindingFlags.Public | BindingFlags.Instance)?.SetValue(mp, 3);
        Log.Info("cstexhearts gave 3 Example life fruits and mana crystals");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        try
        {
            if (frame == 30)
            {
                Main.dayTime = true;
                Main.time = 27000;
                Give(self);
            }
            if (frame == 120) Log.Info($"cstexhearts SHOT low life={self.statLife}/{self.statLifeMax2} mana={self.statMana}/{self.statManaMax2}");
        }
        catch (Exception e) { Log.Info($"cstexhearts frame {frame}: FAIL {e}"); }
        if (frame == End) Log.Info("cstexhearts END");
    }
}
