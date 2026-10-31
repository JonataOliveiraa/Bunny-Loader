using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPlayer;

public class TestFruit : ModItem
{
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 7;

    public override void SetDefaults() => Item.CloneDefaults(ItemID.LifeFruit);
}
