using Terraria;
using Terraria.ModLoader;
using CsNpcAi.Content;

namespace CsNpcAi;

public sealed class CsNpcAiMod : Mod
{
    public override void Load()
    {
        if (ModContent.TryFind<GlobalNPC>(Name + "/AGlobalNpc", out _)) AGlobalNpc.StartObserving();
        On_Player.Update += Update;
    }

    static void Update(On_Player.orig_Update orig, Player player, int index)
    {
        orig(player, index);
        if (Main.gameMenu) Checks.Reset();
        else if (index == Main.myPlayer) Checks.Frame(player);
    }
}
