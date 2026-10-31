using Terraria;
using Terraria.ModLoader;

namespace CsNpc;

public sealed class CsNpcMod : Mod
{
    public static int CountDuringLoad;
    public static bool ContentReady;

    public override void Load()
    {
        CountDuringLoad = NPCLoader.NPCCount;
        On_Player.Update += Update;
    }

    public override void PostSetupContent() => ContentReady = true;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu) Checks.Reset();
        else if (i == Main.myPlayer) Checks.Frame(self);
    }
}
