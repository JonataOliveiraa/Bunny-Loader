using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropLocalPerClientAndResetsNPCMoneyTo0 : CommonDrop
{
    internal DropLocalPerClientAndResetsNPCMoneyTo0(IntPtr pointer) : base(pointer, typeof(DropLocalPerClientAndResetsNPCMoneyTo0)) { }
    public DropLocalPerClientAndResetsNPCMoneyTo0(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum, IItemDropRuleCondition optionalCondition) : base(N.DropLocalPerClientAndResetsNPCMoneyTo0.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, DropRuleInterop.ToNative(optionalCondition)).Pointer, typeof(DropLocalPerClientAndResetsNPCMoneyTo0)) { }
    public IItemDropRuleCondition condition { get => DropRuleInterop.Wrap(new N.DropLocalPerClientAndResetsNPCMoneyTo0(Prototype).condition); set => new N.DropLocalPerClientAndResetsNPCMoneyTo0(Prototype).condition = DropRuleInterop.ToNative(value); }
}
