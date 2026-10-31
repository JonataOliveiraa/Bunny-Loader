using Terraria;
using Terraria.ModLoader;

namespace CsModSystem;

public sealed class CsModSystemMod : Mod
{
    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            Checks.Reset();
            return;
        }
        if (i != Main.myPlayer) return;
        Checks.Frame();
    }
}
