using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMinion;

public sealed class CsMinionMod : Mod
{
    static int frames, styleCalls, pass, fail;
    static bool done;
    static string diffs = "";

    static ILog Log => ModContent.GetInstance<CsMinionMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.ItemCheck += ItemCheck;
        On_Player.ItemCheck_ApplyUseStyle += ApplyUseStyle;
    }

    static void ApplyUseStyle(On_Player.orig_ItemCheck_ApplyUseStyle orig, Player self, float mountOffset, Item sItem, Rectangle heldItemFrame)
    {
        if (styleCalls++ == 0 && self.whoAmI == Main.myPlayer && !Main.gameMenu) diffs = Compare(sItem, self.inventory[self.selectedItem]);
        orig(self, mountOffset, sItem, heldItemFrame);
    }

    static string Compare(Item visual, Item held)
    {
        var found = new List<string>();
        void Cmp(string name, object a, object b) { if (!Equals(a, b)) found.Add($"{name}:{a}/{b}"); }
        Cmp("type", visual.type, held.type); Cmp("noMelee", visual.noMelee, held.noMelee); Cmp("summon", visual.summon, held.summon);
        Cmp("autoReuse", visual.autoReuse, held.autoReuse); Cmp("width", visual.width, held.width); Cmp("height", visual.height, held.height);
        Cmp("buffType", visual.buffType, held.buffType); Cmp("rare", visual.rare, held.rare); Cmp("value", visual.value, held.value);
        Cmp("damage", visual.damage, held.damage); Cmp("useStyle", visual.useStyle, held.useStyle); Cmp("shoot", visual.shoot, held.shoot);
        Cmp("mana", visual.mana, held.mana); Cmp("scale", visual.scale, held.scale); Cmp("prefix", visual.prefix, held.prefix);
        return string.Join(" ", found);
    }

    static bool Using => frames >= 60 && frames < 100;

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (!Main.gameMenu && self.whoAmI == Main.myPlayer && Using)
        {
            self.controlUseItem = true;
            self.releaseUseItem = frames == 60;
        }
        orig(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frames = styleCalls = pass = fail = 0;
            done = false;
            diffs = "";
            orig(self, i);
            return;
        }
        if (i == Main.myPlayer && !done)
        {
            frames++;
            if (frames == 30)
            {
                self.inventory[self.selectedItem].SetDefaults(ItemID.SlimeStaff, default);
                styleCalls = 0;
            }
        }
        orig(self, i);
        if (i != Main.myPlayer || done || frames != 240) return;
        int minions = 0;
        for (int p = 0; p < Main.maxProjectiles; p++)
            if (Main.projectile[p].active && Main.projectile[p].owner == Main.myPlayer && Main.projectile[p].minion) minions++;
        Report("the summon staff spawns a minion", minions > 0, $"minions={minions}");
        Report("the item the use style sees is a full copy of the held item", styleCalls > 0 && diffs == "", $"calls={styleCalls} diffs='{diffs}'");
        Log.Info($"csminion END pass={pass} fail={fail}");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csminion {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
