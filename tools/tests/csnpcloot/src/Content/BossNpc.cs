using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class BossNpc : LootNpc
{
    public static bool Empty;
    public static int Calls;
    public override void BossLoot(ref int potionType, ref int potionStack, ref int heartStack)
    {
        Calls++;
        potionType = ItemID.Ruby;
        potionStack = Empty ? 0 : 2;
        heartStack = Empty ? 0 : 3;
    }
}
