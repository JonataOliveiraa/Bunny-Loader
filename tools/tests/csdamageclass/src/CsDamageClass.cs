using Terraria;
using Terraria.ModLoader;

namespace CsDamageClass;

public sealed class CsDamageClassMod : Mod
{
    public override void Load()
    {
        On_Player.UpdateEquips += UpdateEquips;
        On_Player.Update += Update;
    }

    static void UpdateEquips(On_Player.orig_UpdateEquips orig, Player self, int i)
    {
        orig(self, i);
        if (i == Main.myPlayer) Steps.Bonus?.Invoke(self);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (i == Main.myPlayer && !Main.gameMenu) Steps.Tick(self);
    }
}
