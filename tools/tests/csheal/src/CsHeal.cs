using Terraria;
using Terraria.ModLoader;

namespace CsHeal;

public sealed class CsHealMod : Mod
{
    static int frames;
    static bool done;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            done = false;
            frames = 0;
            return;
        }
        if (done || i != Main.myPlayer || ++frames != 120) return;
        done = true;
        Checks.Run(self);
    }
}
