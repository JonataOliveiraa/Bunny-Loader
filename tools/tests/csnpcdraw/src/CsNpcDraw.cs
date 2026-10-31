using Terraria;
using Terraria.ModLoader;

namespace CsNpcDraw;

public sealed class CsNpcDrawMod : Mod
{
    public static int ExtraHead = -1;

    public override void Load()
    {
        ExtraHead = AddBossHeadTexture("CsNpcDraw/Content/ExtraHead");
        On_Player.Update += Update;
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
