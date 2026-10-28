using Terraria;
using Terraria.ModLoader;

namespace CsSave;

public sealed class CsSaveMod : Mod
{
    static int frames;
    static bool done;

    public override void Load()
    {
        TagChecks.Run();
        On_Player.Update += Update;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || i != Main.myPlayer || Main.gameMenu || ++frames != 60) return;
        done = true;
        WorldCheck.Run(self);
    }
}
