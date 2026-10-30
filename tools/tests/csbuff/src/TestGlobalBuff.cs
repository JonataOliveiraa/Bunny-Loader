using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBuff;

public class TestGlobalBuff : GlobalBuff
{
    public static int VanillaUpdates;
    public static bool KeepVanilla;

    public override void Update(int type, Player player, ref int buffIndex)
    {
        if (type == BuffID.Regeneration) VanillaUpdates++;
    }

    public override void ModifyBuffText(int type, ref string buffName, ref string tip, ref int rare)
    {
        if (type == BuffID.Regeneration) buffName = "[g] " + buffName;
    }

    public override bool RightClick(int type, int buffIndex) => !(KeepVanilla && type == BuffID.Regeneration);
}
