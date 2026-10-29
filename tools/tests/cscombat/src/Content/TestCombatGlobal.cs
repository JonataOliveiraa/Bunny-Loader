using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCombat.Content;

public class TestCombatGlobal : GlobalItem
{
    public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
    {
        if (Checks.On && item.type == ItemID.CopperShortsword) damage *= 1.5f;
    }
}
