using System.Collections.Generic;

namespace Terraria.GameContent.ItemDropRules;

public static class Chains
{
    public class TryIfSucceeded : IItemDropRuleChainAttempt
    {
        public IItemDropRule RuleToChain { get; }
        public bool hideLootReport;
        public TryIfSucceeded(IItemDropRule rule, bool hideLootReport = false) { RuleToChain = rule; this.hideLootReport = hideLootReport; }
        public virtual bool CanChainIntoRule(ItemDropAttemptResult parentResult) => parentResult.State == ItemDropAttemptResultState.Success;
        public virtual void ReportDroprates(float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
        {
            if (!hideLootReport) RuleToChain.ReportDroprates(drops, ratesInfo.With(personalDropRate));
        }
    }

    public class TryIfFailedRandomRoll : IItemDropRuleChainAttempt
    {
        public IItemDropRule RuleToChain { get; }
        public bool hideLootReport;
        public TryIfFailedRandomRoll(IItemDropRule rule, bool hideLootReport = false) { RuleToChain = rule; this.hideLootReport = hideLootReport; }
        public virtual bool CanChainIntoRule(ItemDropAttemptResult parentResult) => parentResult.State == ItemDropAttemptResultState.FailedRandomRoll;
        public virtual void ReportDroprates(float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
        {
            if (!hideLootReport) RuleToChain.ReportDroprates(drops, ratesInfo.With(1f - personalDropRate));
        }
    }

    public class TryIfDoesntFillConditions : IItemDropRuleChainAttempt
    {
        public IItemDropRule RuleToChain { get; }
        public bool hideLootReport;
        public TryIfDoesntFillConditions(IItemDropRule rule, bool hideLootReport = false) { RuleToChain = rule; this.hideLootReport = hideLootReport; }
        public virtual bool CanChainIntoRule(ItemDropAttemptResult parentResult) => parentResult.State == ItemDropAttemptResultState.DoesntFillConditions;
        public virtual void ReportDroprates(float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
        {
            if (!hideLootReport) RuleToChain.ReportDroprates(drops, ratesInfo.With(personalDropRate));
        }
    }

    public static IItemDropRule OnSuccess(this IItemDropRule rule, IItemDropRule ruleToChain, bool hideLootReport = false)
    {
        rule.ChainedRules.Add(new TryIfSucceeded(ruleToChain, hideLootReport));
        return ruleToChain;
    }

    public static IItemDropRule OnFailedRoll(this IItemDropRule rule, IItemDropRule ruleToChain, bool hideLootReport = false)
    {
        rule.ChainedRules.Add(new TryIfFailedRandomRoll(ruleToChain, hideLootReport));
        return ruleToChain;
    }

    public static IItemDropRule OnFailedConditions(this IItemDropRule rule, IItemDropRule ruleToChain, bool hideLootReport = false)
    {
        rule.ChainedRules.Add(new TryIfDoesntFillConditions(ruleToChain, hideLootReport));
        return ruleToChain;
    }

    public static void ReportDroprates(List<IItemDropRuleChainAttempt> chainedRules, float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
    {
        foreach (IItemDropRuleChainAttempt chain in chainedRules) chain.ReportDroprates(personalDropRate, drops, ratesInfo);
    }
}
