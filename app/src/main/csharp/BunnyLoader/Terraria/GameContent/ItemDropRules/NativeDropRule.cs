using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ModLoader.Core;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class NativeDropRule : IItemDropRule
{
    readonly GameHandle handle;
    readonly Type nativeType;
    List<IItemDropRuleChainAttempt>? chains;

    protected internal NativeDropRule(IntPtr pointer, Type nativeType)
    {
        handle = new GameHandle(pointer);
        this.nativeType = nativeType;
    }

    internal IntPtr Prototype => handle.Target;
    internal bool UsesNativeImplementation => GetType() == nativeType;
    internal List<IItemDropRuleChainAttempt>? EditedChains => chains;

    public List<IItemDropRuleChainAttempt> ChainedRules
    {
        get
        {
            if (chains == null)
            {
                chains = DropRuleInterop.ReadChains(new N.IItemDropRule(Prototype).ChainedRules);
                DropRuleInterop.Watch(this);
            }
            return chains;
        }
    }

    public virtual bool CanDrop(DropAttemptInfo info)
    {
        using var scope = new ItemLootHooks.AttemptScope(info);
        return new N.IItemDropRule(Prototype).CanDrop(DropRuleInterop.ToNative(info));
    }

    public virtual ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
    {
        using var scope = new ItemLootHooks.AttemptScope(info);
        return DropRuleInterop.ToManaged(new N.IItemDropRule(Prototype).TryDroppingItem(DropRuleInterop.ToNative(info)));
    }

    public virtual void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
    {
        DropRuleInterop.Sync(this, Prototype);
        DropRuleInterop.Report(Prototype, drops, ratesInfo);
    }
}
