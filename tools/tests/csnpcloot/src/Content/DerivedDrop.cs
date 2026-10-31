using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class DerivedDrop : CommonDrop
{
    public static int Calls;
    public DerivedDrop() : base(ItemID.Diamond, 1) { }
    public override bool CanDrop(DropAttemptInfo info) { Calls++; return false; }
}
