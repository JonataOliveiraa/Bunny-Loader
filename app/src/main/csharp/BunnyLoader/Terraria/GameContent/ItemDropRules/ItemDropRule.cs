using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public static class ItemDropRule
{
    public static IItemDropRule Common(int itemId, int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.Common(itemId, chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule BossBag(int itemId) => DropRuleInterop.Wrap(N.ItemDropRule.BossBag(itemId));
    public static IItemDropRule BossBagByCondition(IItemDropRuleCondition condition, int itemId) => DropRuleInterop.Wrap(N.ItemDropRule.BossBagByCondition(DropRuleInterop.ToNative(condition), itemId));
    public static IItemDropRule ExpertGetsRerolls(int itemId, int chanceDenominator, int expertRerolls) => DropRuleInterop.Wrap(N.ItemDropRule.ExpertGetsRerolls(itemId, chanceDenominator, expertRerolls));
    public static IItemDropRule MasterModeCommonDrop(int itemId) => DropRuleInterop.Wrap(N.ItemDropRule.MasterModeCommonDrop(itemId));
    public static IItemDropRule MasterModeDropOnAllPlayers(int itemId, int chanceDenominator = 1) => DropRuleInterop.Wrap(N.ItemDropRule.MasterModeDropOnAllPlayers(itemId, chanceDenominator));
    public static IItemDropRule WithRerolls(int itemId, int rerolls, int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.WithRerolls(itemId, rerolls, chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule ByCondition(IItemDropRuleCondition condition, int itemId, int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1, int chanceNumerator = 1) => DropRuleInterop.Wrap(N.ItemDropRule.ByCondition(DropRuleInterop.ToNative(condition), itemId, chanceDenominator, minimumDropped, maximumDropped, chanceNumerator));
    public static IItemDropRule ScalingWithOnlyBadLuck(int itemId, int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.ScalingWithOnlyBadLuck(itemId, chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule NotScalingWithLuck(int itemId, int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.NotScalingWithLuck(itemId, chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule OneFromOptionsNotScalingWithLuck(int chanceDenominator, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.OneFromOptionsNotScalingWithLuck(chanceDenominator, DropRuleInterop.ToNative(options)));
    public static IItemDropRule OneFromOptionsNotScalingWithLuckWithX(int chanceDenominator, int chanceNumerator, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.OneFromOptionsNotScalingWithLuckWithX(chanceDenominator, chanceNumerator, DropRuleInterop.ToNative(options)));
    public static IItemDropRule OneFromOptions(int chanceDenominator, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.OneFromOptions(chanceDenominator, DropRuleInterop.ToNative(options)));
    public static IItemDropRule OneFromOptionsWithNumerator(int chanceDenominator, int chanceNumerator, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.OneFromOptionsWithNumerator(chanceDenominator, chanceNumerator, DropRuleInterop.ToNative(options)));
    public static IItemDropRule DropNothing() => DropRuleInterop.Wrap(N.ItemDropRule.DropNothing());
    public static IItemDropRule Gel(int chanceDenominator = 1, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.Gel(chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule NormalvsExpert(int itemId, int chanceDenominatorInNormal, int chanceDenominatorInExpert) => DropRuleInterop.Wrap(N.ItemDropRule.NormalvsExpert(itemId, chanceDenominatorInNormal, chanceDenominatorInExpert));
    public static IItemDropRule NormalvsExpertNotScalingWithLuck(int itemId, int chanceDenominatorInNormal, int chanceDenominatorInExpert) => DropRuleInterop.Wrap(N.ItemDropRule.NormalvsExpertNotScalingWithLuck(itemId, chanceDenominatorInNormal, chanceDenominatorInExpert));
    public static IItemDropRule NormalvsExpertOneFromOptionsNotScalingWithLuck(int chanceDenominatorInNormal, int chanceDenominatorInExpert, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.NormalvsExpertOneFromOptionsNotScalingWithLuck(chanceDenominatorInNormal, chanceDenominatorInExpert, DropRuleInterop.ToNative(options)));
    public static IItemDropRule NormalvsExpertOneFromOptions(int chanceDenominatorInNormal, int chanceDenominatorInExpert, params int[] options) => DropRuleInterop.Wrap(N.ItemDropRule.NormalvsExpertOneFromOptions(chanceDenominatorInNormal, chanceDenominatorInExpert, DropRuleInterop.ToNative(options)));
    public static IItemDropRule Food(int itemId, int chanceDenominator, int minimumDropped = 1, int maximumDropped = 1) => DropRuleInterop.Wrap(N.ItemDropRule.Food(itemId, chanceDenominator, minimumDropped, maximumDropped));
    public static IItemDropRule StatusImmunityItem(int itemId, int dropsOutOfX) => DropRuleInterop.Wrap(N.ItemDropRule.StatusImmunityItem(itemId, dropsOutOfX));
}
