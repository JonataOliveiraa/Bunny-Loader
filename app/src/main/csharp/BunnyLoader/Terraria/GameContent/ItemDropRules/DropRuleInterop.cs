using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ModLoader.Core;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

internal static unsafe class DropRuleInterop
{
    static readonly Dictionary<IntPtr, WeakReference<IItemDropRule>> Rules = new();
    static readonly Dictionary<IntPtr, NativeDropRule> WatchedRules = new();
    static readonly Dictionary<IntPtr, WeakReference<IItemDropRuleCondition>> ConditionCache = new();
    static readonly Dictionary<IItemDropRule, GameHandle> CustomRulePointers = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<IntPtr, IItemDropRule> CustomRules = new();
    static readonly Dictionary<IItemDropRuleCondition, GameHandle> CustomConditionPointers = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<IntPtr, IItemDropRuleCondition> CustomConditions = new();
    static readonly Dictionary<IItemDropRuleChainAttempt, GameHandle> ChainPointers = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<IntPtr, IItemDropRuleChainAttempt> CustomChains = new();
    static readonly HashSet<IntPtr> Syncing = new();
    static MethodRef reportMethod, rateConstructor;
    static readonly GameClass IntClass = new("System", "Int32");

    internal static void Watch(NativeDropRule rule)
    {
        WantResolver();
        WatchedRules[rule.Prototype] = rule;
        Rules[rule.Prototype] = new(rule);
    }

    internal static N.DropAttemptInfo ToNative(DropAttemptInfo info) => new()
    {
        npc = info.npc.IsNull ? ItemLootHooks.CurrentNPC : info.npc,
        player = info.player,
        rng = info.rng,
        IsInSimulation = info.IsInSimulation,
        IsExpertMode = info.IsExpertMode,
        IsMasterMode = info.IsMasterMode
    };

    internal static DropAttemptInfo ToManaged(N.DropAttemptInfo info) => new()
    {
        npc = ItemLootHooks.IsOpening(info.npc) ? default : info.npc,
        item = ItemLootHooks.IsOpening(info.npc) ? ItemLootHooks.CurrentItem : 0,
        player = info.player,
        rng = info.rng,
        IsInSimulation = info.IsInSimulation,
        IsExpertMode = info.IsExpertMode,
        IsMasterMode = info.IsMasterMode
    };

    internal static ItemDropAttemptResult ToManaged(N.ItemDropAttemptResult result) => new() { State = (ItemDropAttemptResultState)result.State };
    internal static N.ItemDropAttemptResult ToNative(ItemDropAttemptResult result) => new() { State = (N.ItemDropAttemptResultState)result.State };

    internal static GameArray<int> ToNative(int[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var array = GameArray<int>.New(IntClass, values.Length);
        values.AsSpan().CopyTo(array.AsSpan());
        return array;
    }

    internal static int[] ToManaged(GameArray<int> values) => values.IsNull ? Array.Empty<int>() : values.AsSpan().ToArray();

    internal static GameArray<N.IItemDropRule> ToNative(IItemDropRule[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var array = GameArray<N.IItemDropRule>.New(N.IItemDropRule.Class, values.Length);
        using var pin = new GameHandle(array.Pointer);
        for (int i = 0; i < values.Length; i++) array[i] = ToNative(values[i]);
        return array;
    }

    internal static IItemDropRule[] ToManaged(GameArray<N.IItemDropRule> values)
    {
        if (values.IsNull) return Array.Empty<IItemDropRule>();
        var result = new IItemDropRule[values.Length];
        for (int i = 0; i < result.Length; i++) result[i] = Wrap(values[i]);
        return result;
    }

    internal static IItemDropRule Wrap(N.IItemDropRule value)
    {
        IntPtr pointer = value.Pointer;
        if (pointer == IntPtr.Zero) throw new ArgumentException("The native drop rule is null.", nameof(value));
        if (CustomRules.TryGetValue(pointer, out IItemDropRule? custom)) return custom;
        if (Rules.TryGetValue(pointer, out var weak) && weak.TryGetTarget(out IItemDropRule? known)) return known;
        IItemDropRule result = WrapRule(pointer);
        Rules[pointer] = new(result);
        return result;
    }

    static IItemDropRule WrapRule(IntPtr pointer)
    {
        GameObject obj = new(pointer);
        if (N.CommonDropNotScalingWithLuck.Is(obj)) return new CommonDropNotScalingWithLuck(pointer);
        if (N.CommonDropScalingWithOnlyBadLuck.Is(obj)) return new CommonDropScalingWithOnlyBadLuck(pointer);
        if (N.CommonDropWithRerolls.Is(obj)) return new CommonDropWithRerolls(pointer);
        if (N.ItemDropWithConditionRule.Is(obj)) return new ItemDropWithConditionRule(pointer);
        if (N.LeadingConditionRule.Is(obj)) return new LeadingConditionRule(pointer);
        if (N.DropBasedOnExpertMode.Is(obj)) return new DropBasedOnExpertMode(pointer);
        if (N.DropBasedOnExtraGel.Is(obj)) return new DropBasedOnExtraGel(pointer);
        if (N.DropBasedOnMasterAndExpertMode.Is(obj)) return new DropBasedOnMasterAndExpertMode(pointer);
        if (N.DropBasedOnMasterMode.Is(obj)) return new DropBasedOnMasterMode(pointer);
        if (N.DropLocalPerClientAndResetsNPCMoneyTo0.Is(obj)) return new DropLocalPerClientAndResetsNPCMoneyTo0(pointer);
        if (N.DropNothing.Is(obj)) return new DropNothing(pointer);
        if (N.DropPerPlayerOnThePlayer.Is(obj)) return new DropPerPlayerOnThePlayer(pointer);
        if (N.FromOptionsWithoutRepeatsDropRule.Is(obj)) return new FromOptionsWithoutRepeatsDropRule(pointer);
        if (N.MechBossSpawnersDropRule.Is(obj)) return new MechBossSpawnersDropRule(pointer);
        if (N.OneFromOptionsDropRule.Is(obj)) return new OneFromOptionsDropRule(pointer);
        if (N.OneFromOptionsNotScaledWithLuckDropRule.Is(obj)) return new OneFromOptionsNotScaledWithLuckDropRule(pointer);
        if (N.OneFromRulesRule.Is(obj)) return new OneFromRulesRule(pointer);
        if (N.SlimeBodyItemDropRule.Is(obj)) return new SlimeBodyItemDropRule(pointer);
        if (N.StatueMimicItemDropRule.Is(obj)) return new StatueMimicItemDropRule(pointer);
        if (N.CommonDrop.Is(obj)) return new CommonDrop(pointer);
        return new NativeDropRule(pointer, typeof(NativeDropRule));
    }

    internal static IItemDropRuleCondition Wrap(N.IItemDropRuleCondition value)
    {
        IntPtr pointer = value.Pointer;
        if (pointer == IntPtr.Zero) return null!;
        if (CustomConditions.TryGetValue(pointer, out IItemDropRuleCondition? custom)) return custom;
        if (ConditionCache.TryGetValue(pointer, out var weak) && weak.TryGetTarget(out IItemDropRuleCondition? known)) return known;
        IItemDropRuleCondition result = WrapCondition(pointer);
        ConditionCache[pointer] = new(result);
        return result;
    }

    static IItemDropRuleCondition WrapCondition(IntPtr pointer)
    {
        GameObject obj = new(pointer);
        if (N.Conditions.BeatAnyMechBoss.Is(obj)) return new Conditions.BeatAnyMechBoss(pointer);
        if (N.Conditions.CorruptKeyCondition.Is(obj)) return new Conditions.CorruptKeyCondition(pointer);
        if (N.Conditions.CrimsonKeyCondition.Is(obj)) return new Conditions.CrimsonKeyCondition(pointer);
        if (N.Conditions.DesertKeyCondition.Is(obj)) return new Conditions.DesertKeyCondition(pointer);
        if (N.Conditions.DontStarveIsNotUp.Is(obj)) return new Conditions.DontStarveIsNotUp(pointer);
        if (N.Conditions.DontStarveIsUp.Is(obj)) return new Conditions.DontStarveIsUp(pointer);
        if (N.Conditions.DownedAllMechBosses.Is(obj)) return new Conditions.DownedAllMechBosses(pointer);
        if (N.Conditions.DownedPlantera.Is(obj)) return new Conditions.DownedPlantera(pointer);
        if (N.Conditions.DropExtraGel.Is(obj)) return new Conditions.DropExtraGel(pointer);
        if (N.Conditions.Easymode.Is(obj)) return new Conditions.Easymode(pointer);
        if (N.Conditions.EmpressOfLightIsGenuinelyEnraged.Is(obj)) return new Conditions.EmpressOfLightIsGenuinelyEnraged(pointer);
        if (N.Conditions.EyeOfCthulhuDefeatedAndNoAltarsInWorld.Is(obj)) return new Conditions.EyeOfCthulhuDefeatedAndNoAltarsInWorld(pointer);
        if (N.Conditions.FirstTimeKillingPlantera.Is(obj)) return new Conditions.FirstTimeKillingPlantera(pointer);
        if (N.Conditions.FromCertainWaveAndAbove.Is(obj)) return new Conditions.FromCertainWaveAndAbove(pointer);
        if (N.Conditions.FrostMoonDropGateForTrophies.Is(obj)) return new Conditions.FrostMoonDropGateForTrophies(pointer);
        if (N.Conditions.FrostMoonDropGatingChance.Is(obj)) return new Conditions.FrostMoonDropGatingChance(pointer);
        if (N.Conditions.FrozenKeyCondition.Is(obj)) return new Conditions.FrozenKeyCondition(pointer);
        if (N.Conditions.HallowKeyCondition.Is(obj)) return new Conditions.HallowKeyCondition(pointer);
        if (N.Conditions.HalloweenGoodieBagDrop.Is(obj)) return new Conditions.HalloweenGoodieBagDrop(pointer);
        if (N.Conditions.HalloweenWeapons.Is(obj)) return new Conditions.HalloweenWeapons(pointer);
        if (N.Conditions.IsBloodMoonAndNotFromStatue.Is(obj)) return new Conditions.IsBloodMoonAndNotFromStatue(pointer);
        if (N.Conditions.IsChristmas.Is(obj)) return new Conditions.IsChristmas(pointer);
        if (N.Conditions.IsCorruption.Is(obj)) return new Conditions.IsCorruption(pointer);
        if (N.Conditions.IsCorruptionAndNotExpert.Is(obj)) return new Conditions.IsCorruptionAndNotExpert(pointer);
        if (N.Conditions.IsCrimson.Is(obj)) return new Conditions.IsCrimson(pointer);
        if (N.Conditions.IsCrimsonAndNotExpert.Is(obj)) return new Conditions.IsCrimsonAndNotExpert(pointer);
        if (N.Conditions.IsExpert.Is(obj)) return new Conditions.IsExpert(pointer);
        if (N.Conditions.IsHardmode.Is(obj)) return new Conditions.IsHardmode(pointer);
        if (N.Conditions.IsMasterMode.Is(obj)) return new Conditions.IsMasterMode(pointer);
        if (N.Conditions.IsPumpkinMoon.Is(obj)) return new Conditions.IsPumpkinMoon(pointer);
        if (N.Conditions.IsUsingSpecificAIValues.Is(obj)) return new Conditions.IsUsingSpecificAIValues(pointer);
        if (N.Conditions.JungleKeyCondition.Is(obj)) return new Conditions.JungleKeyCondition(pointer);
        if (N.Conditions.LegacyHack_IsABoss.Is(obj)) return new Conditions.LegacyHack_IsABoss(pointer);
        if (N.Conditions.LegacyHack_IsBossAndExpert.Is(obj)) return new Conditions.LegacyHack_IsBossAndExpert(pointer);
        if (N.Conditions.LegacyHack_IsBossAndNotExpert.Is(obj)) return new Conditions.LegacyHack_IsBossAndNotExpert(pointer);
        if (N.Conditions.LivingFlames.Is(obj)) return new Conditions.LivingFlames(pointer);
        if (N.Conditions.MechanicalBossesDummyCondition.Is(obj)) return new Conditions.MechanicalBossesDummyCondition(pointer);
        if (N.Conditions.MechdusaKill.Is(obj)) return new Conditions.MechdusaKill(pointer);
        if (N.Conditions.MissingTwin.Is(obj)) return new Conditions.MissingTwin(pointer);
        if (N.Conditions.NamedNPC.Is(obj)) return new Conditions.NamedNPC(pointer);
        if (N.Conditions.NeverTrue.Is(obj)) return new Conditions.NeverTrue(pointer);
        if (N.Conditions.NotDropExtraGel.Is(obj)) return new Conditions.NotDropExtraGel(pointer);
        if (N.Conditions.NotExpert.Is(obj)) return new Conditions.NotExpert(pointer);
        if (N.Conditions.NotFromStatue.Is(obj)) return new Conditions.NotFromStatue(pointer);
        if (N.Conditions.NotMasterMode.Is(obj)) return new Conditions.NotMasterMode(pointer);
        if (N.Conditions.NotRemixSeed.Is(obj)) return new Conditions.NotRemixSeed(pointer);
        if (N.Conditions.NotRemixSeedEasymode.Is(obj)) return new Conditions.NotRemixSeedEasymode(pointer);
        if (N.Conditions.NotRemixSeedHardmode.Is(obj)) return new Conditions.NotRemixSeedHardmode(pointer);
        if (N.Conditions.PirateMap.Is(obj)) return new Conditions.PirateMap(pointer);
        if (N.Conditions.PlayerNeedsHealing.Is(obj)) return new Conditions.PlayerNeedsHealing(pointer);
        if (N.Conditions.PumpkinMoonDropGateForTrophies.Is(obj)) return new Conditions.PumpkinMoonDropGateForTrophies(pointer);
        if (N.Conditions.PumpkinMoonDropGatingChance.Is(obj)) return new Conditions.PumpkinMoonDropGatingChance(pointer);
        if (N.Conditions.RedHatSkeletron.Is(obj)) return new Conditions.RedHatSkeletron(pointer);
        if (N.Conditions.RemixSeed.Is(obj)) return new Conditions.RemixSeed(pointer);
        if (N.Conditions.RemixSeedEasymode.Is(obj)) return new Conditions.RemixSeedEasymode(pointer);
        if (N.Conditions.RemixSeedHardmode.Is(obj)) return new Conditions.RemixSeedHardmode(pointer);
        if (N.Conditions.SkyblockIsNotUp.Is(obj)) return new Conditions.SkyblockIsNotUp(pointer);
        if (N.Conditions.SkyblockIsUp.Is(obj)) return new Conditions.SkyblockIsUp(pointer);
        if (N.Conditions.SkyblockIsUpNoSickle.Is(obj)) return new Conditions.SkyblockIsUpNoSickle(pointer);
        if (N.Conditions.SoulOfLight.Is(obj)) return new Conditions.SoulOfLight(pointer);
        if (N.Conditions.SoulOfNight.Is(obj)) return new Conditions.SoulOfNight(pointer);
        if (N.Conditions.TenthAnniversaryIsNotUp.Is(obj)) return new Conditions.TenthAnniversaryIsNotUp(pointer);
        if (N.Conditions.TenthAnniversaryIsUp.Is(obj)) return new Conditions.TenthAnniversaryIsUp(pointer);
        if (N.Conditions.WindyEnoughForKiteDrops.Is(obj)) return new Conditions.WindyEnoughForKiteDrops(pointer);
        if (N.Conditions.XmasPresentDrop.Is(obj)) return new Conditions.XmasPresentDrop(pointer);
        if (N.Conditions.YoyoCascade.Is(obj)) return new Conditions.YoyoCascade(pointer);
        if (N.Conditions.YoyosAmarok.Is(obj)) return new Conditions.YoyosAmarok(pointer);
        if (N.Conditions.YoyosHelFire.Is(obj)) return new Conditions.YoyosHelFire(pointer);
        if (N.Conditions.YoyosKraken.Is(obj)) return new Conditions.YoyosKraken(pointer);
        if (N.Conditions.YoyosYelets.Is(obj)) return new Conditions.YoyosYelets(pointer);
        return new NativeDropCondition(pointer, typeof(NativeDropCondition));
    }

    internal static N.IItemDropRule ToNative(IItemDropRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        WantResolver();
        IntPtr pointer;
        if (rule is NativeDropRule native && native.UsesNativeImplementation) pointer = native.Prototype;
        else
        {
            if (!CustomRulePointers.TryGetValue(rule, out GameHandle? handle))
            {
                pointer = N.CommonDrop.New(0, 1).Pointer;
                handle = new GameHandle(pointer);
                CustomRulePointers.Add(rule, handle);
                CustomRules.Add(pointer, rule);
                ItemHooks.Once("CustomDropReports", () => N.On_CommonDrop.ReportDroprates += ReportCustomRule);
            }
            pointer = handle.Target;
        }
        Rules[pointer] = new(rule);
        Sync(rule, pointer);
        return new(pointer);
    }

    internal static N.IItemDropRuleCondition ToNative(IItemDropRuleCondition? condition)
    {
        if (condition == null) return default;
        if (condition is NativeDropCondition native && native.UsesNativeImplementation) return new(native.Prototype);
        if (!CustomConditionPointers.TryGetValue(condition, out GameHandle? handle))
        {
            ItemHooks.Once("CustomDropConditions", () =>
            {
                N.On_Conditions_NeverTrue.CanDrop += (orig, self, info) => CustomConditions.TryGetValue(self.Pointer, out var own) ? own.CanDrop(ToManaged(info)) : orig(self, info);
                N.On_Conditions_NeverTrue.CanShowItemDropInUI += (orig, self) => CustomConditions.TryGetValue(self.Pointer, out var own) ? own.CanShowItemDropInUI() : orig(self);
                N.On_Conditions_NeverTrue.GetConditionDescription += (orig, self) => CustomConditions.TryGetValue(self.Pointer, out var own) ? own.GetConditionDescription() : orig(self);
            });
            handle = new GameHandle(N.Conditions.NeverTrue.New().Pointer);
            CustomConditionPointers.Add(condition, handle);
            CustomConditions.Add(handle.Target, condition);
        }
        return new(handle.Target);
    }

    static void WantResolver() => ItemHooks.Once("ManagedDropResolver", () => On_ItemDropResolver.ResolveRule += ResolveRule);

    static N.ItemDropAttemptResult ResolveRule(On_ItemDropResolver.orig_ResolveRule orig, ItemDropResolver resolver, N.IItemDropRule rule, N.DropAttemptInfo info)
    {
        if (Rules.TryGetValue(rule.Pointer, out var weak) && weak.TryGetTarget(out var tracked)) Sync(tracked, rule.Pointer);
        if (!CustomRules.TryGetValue(rule.Pointer, out IItemDropRule? custom)) return orig(resolver, rule, info);
        DropAttemptInfo managed = ToManaged(info);
        ItemDropAttemptResult result;
        try
        {
            result = !custom.CanDrop(managed) ? new() { State = ItemDropAttemptResultState.DoesntFillConditions }
                : custom is INestedItemDropRule nested ? nested.TryDroppingItem(managed, (child, attempt) => resolver.ResolveRule(child, attempt))
                : custom.TryDroppingItem(managed);
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{custom.GetType().FullName}.TryDroppingItem", e);
            result = new() { State = ItemDropAttemptResultState.DidNotRunCode };
        }
        N.ItemDropAttemptResult nativeResult = ToNative(result);
        resolver.ResolveRuleChains(rule, info, nativeResult);
        return nativeResult;
    }

    internal static void Sync(IItemDropRule rule, IntPtr pointer)
    {
        List<IItemDropRuleChainAttempt>? chains = rule is NativeDropRule native && native.UsesNativeImplementation ? native.EditedChains : rule.ChainedRules;
        if (chains == null || !Syncing.Add(pointer)) return;
        try
        {
            GameObject list = new N.IItemDropRule(pointer).ChainedRules;
            Clear(list);
            var game = new GameList(list);
            foreach (IItemDropRuleChainAttempt chain in chains) game.Add(ToNative(chain).Pointer);
        }
        finally { Syncing.Remove(pointer); }
    }

    internal static void Clear(GameObject list)
    {
        byte* name = Host.Utf8("Clear");
        IntPtr method;
        try { method = Host.Native()->FindMethod(*(IntPtr*)list.Pointer, name, 0); }
        finally { Host.FreeUtf8(name); }
        if (method == IntPtr.Zero) throw new MissingMethodException("The native list has no Clear method.");
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list.Pointer);
        call.Arg(method);
        call.Invoke(Host.Native()->MethodPointer(method));
    }

    internal static List<IItemDropRule> ReadRules(GameObject list)
    {
        var game = new GameList(list);
        if (game.IsNull) return new();
        var result = new List<IItemDropRule>(game.Count);
        for (int i = 0; i < game.Count; i++) result.Add(Wrap(new N.IItemDropRule(game[i])));
        return result;
    }

    internal static List<IItemDropRuleChainAttempt> ReadChains(GameObject list)
    {
        var game = new GameList(list);
        if (game.IsNull) return new();
        var result = new List<IItemDropRuleChainAttempt>(game.Count);
        for (int i = 0; i < game.Count; i++) result.Add(new NativeDropChain(game[i]));
        return result;
    }

    static N.IItemDropRuleChainAttempt ToNative(IItemDropRuleChainAttempt chain)
    {
        if (chain is NativeDropChain native) return new(native.Pointer);
        if (ChainPointers.TryGetValue(chain, out GameHandle? known))
        {
            if (chain.GetType() == typeof(Chains.TryIfSucceeded)) new N.Chains.TryIfSucceeded(known.Target).hideLootReport = ((Chains.TryIfSucceeded)chain).hideLootReport;
            if (chain.GetType() == typeof(Chains.TryIfFailedRandomRoll)) new N.Chains.TryIfFailedRandomRoll(known.Target).hideLootReport = ((Chains.TryIfFailedRandomRoll)chain).hideLootReport;
            if (chain.GetType() == typeof(Chains.TryIfDoesntFillConditions)) new N.Chains.TryIfDoesntFillConditions(known.Target).hideLootReport = ((Chains.TryIfDoesntFillConditions)chain).hideLootReport;
            return new(known.Target);
        }
        N.IItemDropRule rule = ToNative(chain.RuleToChain);
        IntPtr pointer;
        if (chain.GetType() == typeof(Chains.TryIfSucceeded)) pointer = N.Chains.TryIfSucceeded.New(rule, ((Chains.TryIfSucceeded)chain).hideLootReport).Pointer;
        else if (chain.GetType() == typeof(Chains.TryIfFailedRandomRoll)) pointer = N.Chains.TryIfFailedRandomRoll.New(rule, ((Chains.TryIfFailedRandomRoll)chain).hideLootReport).Pointer;
        else if (chain.GetType() == typeof(Chains.TryIfDoesntFillConditions)) pointer = N.Chains.TryIfDoesntFillConditions.New(rule, ((Chains.TryIfDoesntFillConditions)chain).hideLootReport).Pointer;
        else
        {
            pointer = N.Chains.TryIfSucceeded.New(rule).Pointer;
            CustomChains.Add(pointer, chain);
            ItemHooks.Once("CustomDropChains", () =>
            {
                N.On_Chains_TryIfSucceeded.get_RuleToChain += (orig, self) => CustomChains.TryGetValue(self.Pointer, out var own) ? ToNative(own.RuleToChain) : orig(self);
                N.On_Chains_TryIfSucceeded.CanChainIntoRule += (orig, self, result) => CustomChains.TryGetValue(self.Pointer, out var own) ? own.CanChainIntoRule(ToManaged(result)) : orig(self, result);
                N.On_Chains_TryIfSucceeded.ReportDroprates += (orig, self, rate, drops, feed) =>
                {
                    if (!CustomChains.TryGetValue(self.Pointer, out var own)) { orig(self, rate, drops, feed); return; }
                    var reports = new List<DropRateInfo>();
                    own.ReportDroprates(rate, reports, ToManaged(feed));
                    AppendRates(drops, reports);
                };
            });
        }
        ChainPointers.Add(chain, new GameHandle(pointer));
        return new(pointer);
    }

    internal static GameObject NewRateList()
    {
        ref readonly MethodRef method = ref GameInterop.Method(ref reportMethod, N.CommonDrop.Class,
            "void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)", [-1, 16, -1]);
        IntPtr result = Host.Native()->NewParameterObject(method.Info, 0);
        if (result == IntPtr.Zero) throw new InvalidOperationException("Could not create the native drop rate list.");
        return new(result);
    }

    static GameObject ToNative(List<IItemDropRuleCondition>? conditions)
    {
        if (conditions == null || conditions.Count == 0) return default;
        ref readonly MethodRef method = ref GameInterop.Method(ref rateConstructor, N.DropRateInfo.Class,
            "void .ctor(int itemId, int stackMin, int stackMax, float dropRate, List<IItemDropRuleCondition> conditions)", [-1, -1, -1, -1, -1, -1]);
        IntPtr pointer = Host.Native()->NewParameterObject(method.Info, 4);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("Could not create the native drop condition list.");
        using var pin = new GameHandle(pointer);
        var game = new GameList(pointer);
        foreach (var condition in conditions) game.Add(ToNative(condition).Pointer);
        return new(pointer);
    }

    static List<IItemDropRuleCondition>? ReadConditions(GameObject conditions)
    {
        if (conditions.IsNull) return null;
        var game = new GameList(conditions);
        var result = new List<IItemDropRuleCondition>(game.Count);
        for (int i = 0; i < game.Count; i++) result.Add(Wrap(new N.IItemDropRuleCondition(game[i])));
        return result;
    }

    static DropRateInfoChainFeed ToManaged(N.DropRateInfoChainFeed feed) => new(feed.parentDroprateChance) { conditions = ReadConditions(feed.conditions) };

    internal static void Report(IntPtr pointer, List<DropRateInfo> drops, DropRateInfoChainFeed feed)
    {
        ArgumentNullException.ThrowIfNull(drops);
        if (Rules.TryGetValue(pointer, out var weak) && weak.TryGetTarget(out var rule)) Sync(rule, pointer);
        GameObject rates = NewRateList();
        using var ratesPin = new GameHandle(rates.Pointer);
        GameObject conditions = ToNative(feed.conditions);
        using var conditionPin = new GameHandle(conditions.Pointer);
        new N.IItemDropRule(pointer).ReportDroprates(rates, new N.DropRateInfoChainFeed { parentDroprateChance = feed.parentDroprateChance, conditions = conditions });
        ReadRates(rates, drops);
    }

    internal static void ReadRates(GameObject rates, List<DropRateInfo> drops)
    {
        var game = new GameList(rates);
        for (int i = 0; i < game.Count; i++)
        {
            N.DropRateInfo rate = game.Get<N.DropRateInfo>(i);
            drops.Add(new DropRateInfo(rate.itemId, rate.stackMin, rate.stackMax, rate.dropRate, ReadConditions(rate.conditions)));
        }
    }

    internal static void AppendRates(GameObject rates, List<DropRateInfo> drops)
    {
        var game = new GameList(rates);
        foreach (DropRateInfo drop in drops)
        {
            GameObject conditions = ToNative(drop.conditions);
            using var pin = new GameHandle(conditions.Pointer);
            game.Add(new N.DropRateInfo { itemId = drop.itemId, stackMin = drop.stackMin, stackMax = drop.stackMax, dropRate = drop.dropRate, conditions = conditions });
        }
    }

    static void ReportCustomRule(N.On_CommonDrop.orig_ReportDroprates orig, N.CommonDrop self, GameObject drops, N.DropRateInfoChainFeed feed)
    {
        if (!CustomRules.TryGetValue(self.Pointer, out var rule)) { orig(self, drops, feed); return; }
        var reports = new List<DropRateInfo>();
        try
        {
            rule.ReportDroprates(reports, ToManaged(feed));
            AppendRates(drops, reports);
        }
        catch (Exception e) { ItemHooks.Report($"{rule.GetType().FullName}.ReportDroprates", e); }
    }
}
