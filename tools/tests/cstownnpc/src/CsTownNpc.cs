using Terraria;
using Terraria.ModLoader;

namespace CsTownNpc;

public sealed class CsTownNpcMod : Mod
{
    public override void Load() => On_Player.Update += Update;
    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu) Checks.Reset();
        else if (i == Main.myPlayer) Checks.Frame(self);
    }
}
