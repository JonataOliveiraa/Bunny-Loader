using Terraria.GameContent.ItemDropRules;

namespace CsNpcLoot.Content;

public sealed class DerivedCondition : Conditions.IsExpert
{
    public static int Calls;
    public override bool CanDrop(DropAttemptInfo info) { Calls++; return true; }
}
