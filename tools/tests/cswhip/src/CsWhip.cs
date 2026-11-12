using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsWhip;

public sealed class CsWhipMod : Mod
{
    const int Window = 180;
    static int frame, pass, fail, round, started, swings, slot, first = -1;
    static bool holding, wasAnimating;
    static Item kept;

    static ILog Log => ModContent.GetInstance<CsWhipMod>().Logger;

    static readonly string[] Whips = { "Terraria/LeatherWhip", "ExampleMod/ExampleWhip", "ExampleMod/ExampleWhipAdvanced" };

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cswhip {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int TypeOf(string name) => name.StartsWith("Terraria/") ? ItemID.BlandWhip : ModContent.Find<ModItem>(name).Type;

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        orig(self);
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && holding)
        {
            bool animating = self.itemAnimation > 0;
            if (animating && !wasAnimating)
            {
                if (first < 0) first = frame;
                if (frame < first + Window) swings++;
            }
            wasAnimating = animating;
        }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || round < 0) return;
        frame++;
        try
        {
            if (frame == 30)
            {
                slot = self.selectedItem;
                kept = self.inventory[slot].Clone();
                Log.Info($"cswhip settings: autoReuseAllWeapons={self.autoReuseAllWeapons} autoReuseGlove={self.autoReuseGlove}");
            }
            if (frame >= 30 && !holding && round < Whips.Length && frame % 120 == 0)
            {
                Item whip = new Item(TypeOf(Whips[round]));
                self.inventory[slot] = whip;
                Log.Info($"cswhip {Whips[round]}: autoReuse={whip.autoReuse} channel={whip.channel} damage={whip.damage} useTime={whip.useTime} summon={whip.DamageType}");
                swings = 0;
                first = -1;
                wasAnimating = false;
                started = frame;
                holding = true;
                Log.Info($"cswhip FHOLD {round + 1} 0.756 0.856 4000");
            }
            else if (holding && (first >= 0 && frame == first + Window + 60 || frame == started + 1200))
            {
                holding = false;
                self.controlUseItem = false;
                Item whip = self.inventory[slot];
                Report($"{Whips[round]} repeats while the attack is held", swings >= (whip.channel ? 2 : 3), $"swings={swings} in {Window} frames after the first useAnimation={whip.useAnimation} type={whip.DamageType}");
                round++;
            }
            if (round == Whips.Length && !holding)
            {
                self.inventory[slot] = kept;
                round = -1;
                Log.Info($"cswhip END pass={pass} fail={fail}");
            }
        }
        catch (Exception e)
        {
            round = -1;
            Log.Info($"cswhip END pass=0 fail=1 {e}");
        }
    }
}
