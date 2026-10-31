using System;
using BunnyLoader.Interop;
using Terraria.ModLoader.Core;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class NativeDropCondition : IItemDropRuleCondition
{
    readonly GameHandle handle;
    readonly Type nativeType;

    protected internal NativeDropCondition(IntPtr pointer, Type nativeType)
    {
        handle = new GameHandle(pointer);
        this.nativeType = nativeType;
    }

    internal IntPtr Prototype => handle.Target;
    internal bool UsesNativeImplementation => GetType() == nativeType;

    public virtual bool CanDrop(DropAttemptInfo info)
    {
        using var scope = new ItemLootHooks.AttemptScope(info);
        return new N.IItemDropRuleCondition(Prototype).CanDrop(DropRuleInterop.ToNative(info));
    }
    public virtual bool CanShowItemDropInUI() => new N.IItemDropRuleCondition(Prototype).CanShowItemDropInUI();
    public virtual string GetConditionDescription() => new N.IProvideItemConditionDescription(Prototype).GetConditionDescription() ?? "";
}
