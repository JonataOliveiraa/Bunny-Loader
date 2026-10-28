using Terraria;
using Terraria.ModLoader;

namespace CsEquip;

public sealed class CsEquipMod : Mod
{
    public override void Load()
    {
        EquipLoader.AddEquipTexture(this, "CsEquip/Content/Items/Cape_Back", EquipType.Back, name: "Cape");
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
