using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;

namespace CsSave;

public sealed class CsSaveMod : Mod
{
    static int frames;
    static bool done;
    static readonly Stopwatch SinceUpdate = Stopwatch.StartNew();

    public override void Load()
    {
        TagChecks.Run();
        On_Player.Update += Update;
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            done = false;
            frames = 0;
            return;
        }
        if (i != Main.myPlayer) return;
        if (SinceUpdate.Elapsed.TotalSeconds > 5)
        {
            done = false;
            frames = 0;
        }
        SinceUpdate.Restart();
        if (done || ++frames != 60) return;
        done = true;
        WorldCheck.Run(self);
    }
}
