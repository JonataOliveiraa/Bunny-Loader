using Terraria;
using Terraria.ModLoader;

namespace CsPrim;

public sealed class CsPrimMod : Mod
{
    const int RunFrame = 120;

    static int frames;

    public static bool Ran;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);

        if (Main.gameMenu)
        {
            frames = 0;
            Ran = false;
            return;
        }

        if (i == Main.myPlayer && !Ran && ++frames == RunFrame) Ran = true;
    }
}
