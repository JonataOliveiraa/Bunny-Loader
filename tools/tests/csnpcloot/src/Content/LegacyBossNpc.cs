using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class LegacyBossNpc : LootNpc
{
    public static int Calls;
    public override void BossLoot(ref int potionType) { Calls++; potionType = ItemID.Emerald; }
}
