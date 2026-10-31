using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public static class Conditions
{
    public class BeatAnyMechBoss : NativeDropCondition
    {
        internal BeatAnyMechBoss(IntPtr pointer) : base(pointer, typeof(BeatAnyMechBoss)) { }
        public BeatAnyMechBoss() : base(N.Conditions.BeatAnyMechBoss.New().Pointer, typeof(BeatAnyMechBoss)) { }

    }
    public class CorruptKeyCondition : NativeDropCondition
    {
        internal CorruptKeyCondition(IntPtr pointer) : base(pointer, typeof(CorruptKeyCondition)) { }
        public CorruptKeyCondition() : base(N.Conditions.CorruptKeyCondition.New().Pointer, typeof(CorruptKeyCondition)) { }

    }
    public class CrimsonKeyCondition : NativeDropCondition
    {
        internal CrimsonKeyCondition(IntPtr pointer) : base(pointer, typeof(CrimsonKeyCondition)) { }
        public CrimsonKeyCondition() : base(N.Conditions.CrimsonKeyCondition.New().Pointer, typeof(CrimsonKeyCondition)) { }

    }
    public class DesertKeyCondition : NativeDropCondition
    {
        internal DesertKeyCondition(IntPtr pointer) : base(pointer, typeof(DesertKeyCondition)) { }
        public DesertKeyCondition() : base(N.Conditions.DesertKeyCondition.New().Pointer, typeof(DesertKeyCondition)) { }

    }
    public class DontStarveIsNotUp : NativeDropCondition
    {
        internal DontStarveIsNotUp(IntPtr pointer) : base(pointer, typeof(DontStarveIsNotUp)) { }
        public DontStarveIsNotUp() : base(N.Conditions.DontStarveIsNotUp.New().Pointer, typeof(DontStarveIsNotUp)) { }

    }
    public class DontStarveIsUp : NativeDropCondition
    {
        internal DontStarveIsUp(IntPtr pointer) : base(pointer, typeof(DontStarveIsUp)) { }
        public DontStarveIsUp() : base(N.Conditions.DontStarveIsUp.New().Pointer, typeof(DontStarveIsUp)) { }

    }
    public class DownedAllMechBosses : NativeDropCondition
    {
        internal DownedAllMechBosses(IntPtr pointer) : base(pointer, typeof(DownedAllMechBosses)) { }
        public DownedAllMechBosses() : base(N.Conditions.DownedAllMechBosses.New().Pointer, typeof(DownedAllMechBosses)) { }

    }
    public class DownedPlantera : NativeDropCondition
    {
        internal DownedPlantera(IntPtr pointer) : base(pointer, typeof(DownedPlantera)) { }
        public DownedPlantera() : base(N.Conditions.DownedPlantera.New().Pointer, typeof(DownedPlantera)) { }

    }
    public class DropExtraGel : NativeDropCondition
    {
        internal DropExtraGel(IntPtr pointer) : base(pointer, typeof(DropExtraGel)) { }
        public DropExtraGel() : base(N.Conditions.DropExtraGel.New().Pointer, typeof(DropExtraGel)) { }

    }
    public class Easymode : NativeDropCondition
    {
        internal Easymode(IntPtr pointer) : base(pointer, typeof(Easymode)) { }
        public Easymode() : base(N.Conditions.Easymode.New().Pointer, typeof(Easymode)) { }

    }
    public class EmpressOfLightIsGenuinelyEnraged : NativeDropCondition
    {
        internal EmpressOfLightIsGenuinelyEnraged(IntPtr pointer) : base(pointer, typeof(EmpressOfLightIsGenuinelyEnraged)) { }
        public EmpressOfLightIsGenuinelyEnraged() : base(N.Conditions.EmpressOfLightIsGenuinelyEnraged.New().Pointer, typeof(EmpressOfLightIsGenuinelyEnraged)) { }

    }
    public class EyeOfCthulhuDefeatedAndNoAltarsInWorld : NativeDropCondition
    {
        internal EyeOfCthulhuDefeatedAndNoAltarsInWorld(IntPtr pointer) : base(pointer, typeof(EyeOfCthulhuDefeatedAndNoAltarsInWorld)) { }
        public EyeOfCthulhuDefeatedAndNoAltarsInWorld() : base(N.Conditions.EyeOfCthulhuDefeatedAndNoAltarsInWorld.New().Pointer, typeof(EyeOfCthulhuDefeatedAndNoAltarsInWorld)) { }

    }
    public class FirstTimeKillingPlantera : NativeDropCondition
    {
        internal FirstTimeKillingPlantera(IntPtr pointer) : base(pointer, typeof(FirstTimeKillingPlantera)) { }
        public FirstTimeKillingPlantera() : base(N.Conditions.FirstTimeKillingPlantera.New().Pointer, typeof(FirstTimeKillingPlantera)) { }

    }
    public class FromCertainWaveAndAbove : NativeDropCondition
    {
        internal FromCertainWaveAndAbove(IntPtr pointer) : base(pointer, typeof(FromCertainWaveAndAbove)) { }
        public FromCertainWaveAndAbove(int neededWave) : base(N.Conditions.FromCertainWaveAndAbove.New(neededWave).Pointer, typeof(FromCertainWaveAndAbove)) { }
        public ref int neededWave => ref new N.Conditions.FromCertainWaveAndAbove(Prototype).neededWave;
    }
    public class FrostMoonDropGateForTrophies : NativeDropCondition
    {
        internal FrostMoonDropGateForTrophies(IntPtr pointer) : base(pointer, typeof(FrostMoonDropGateForTrophies)) { }
        public FrostMoonDropGateForTrophies() : base(N.Conditions.FrostMoonDropGateForTrophies.New().Pointer, typeof(FrostMoonDropGateForTrophies)) { }

    }
    public class FrostMoonDropGatingChance : NativeDropCondition
    {
        internal FrostMoonDropGatingChance(IntPtr pointer) : base(pointer, typeof(FrostMoonDropGatingChance)) { }
        public FrostMoonDropGatingChance() : base(N.Conditions.FrostMoonDropGatingChance.New().Pointer, typeof(FrostMoonDropGatingChance)) { }

    }
    public class FrozenKeyCondition : NativeDropCondition
    {
        internal FrozenKeyCondition(IntPtr pointer) : base(pointer, typeof(FrozenKeyCondition)) { }
        public FrozenKeyCondition() : base(N.Conditions.FrozenKeyCondition.New().Pointer, typeof(FrozenKeyCondition)) { }

    }
    public class HallowKeyCondition : NativeDropCondition
    {
        internal HallowKeyCondition(IntPtr pointer) : base(pointer, typeof(HallowKeyCondition)) { }
        public HallowKeyCondition() : base(N.Conditions.HallowKeyCondition.New().Pointer, typeof(HallowKeyCondition)) { }

    }
    public class HalloweenGoodieBagDrop : NativeDropCondition
    {
        internal HalloweenGoodieBagDrop(IntPtr pointer) : base(pointer, typeof(HalloweenGoodieBagDrop)) { }
        public HalloweenGoodieBagDrop() : base(N.Conditions.HalloweenGoodieBagDrop.New().Pointer, typeof(HalloweenGoodieBagDrop)) { }

    }
    public class HalloweenWeapons : NativeDropCondition
    {
        internal HalloweenWeapons(IntPtr pointer) : base(pointer, typeof(HalloweenWeapons)) { }
        public HalloweenWeapons() : base(N.Conditions.HalloweenWeapons.New().Pointer, typeof(HalloweenWeapons)) { }

    }
    public class IsBloodMoonAndNotFromStatue : NativeDropCondition
    {
        internal IsBloodMoonAndNotFromStatue(IntPtr pointer) : base(pointer, typeof(IsBloodMoonAndNotFromStatue)) { }
        public IsBloodMoonAndNotFromStatue() : base(N.Conditions.IsBloodMoonAndNotFromStatue.New().Pointer, typeof(IsBloodMoonAndNotFromStatue)) { }

    }
    public class IsChristmas : NativeDropCondition
    {
        internal IsChristmas(IntPtr pointer) : base(pointer, typeof(IsChristmas)) { }
        public IsChristmas() : base(N.Conditions.IsChristmas.New().Pointer, typeof(IsChristmas)) { }

    }
    public class IsCorruption : NativeDropCondition
    {
        internal IsCorruption(IntPtr pointer) : base(pointer, typeof(IsCorruption)) { }
        public IsCorruption() : base(N.Conditions.IsCorruption.New().Pointer, typeof(IsCorruption)) { }

    }
    public class IsCorruptionAndNotExpert : NativeDropCondition
    {
        internal IsCorruptionAndNotExpert(IntPtr pointer) : base(pointer, typeof(IsCorruptionAndNotExpert)) { }
        public IsCorruptionAndNotExpert() : base(N.Conditions.IsCorruptionAndNotExpert.New().Pointer, typeof(IsCorruptionAndNotExpert)) { }

    }
    public class IsCrimson : NativeDropCondition
    {
        internal IsCrimson(IntPtr pointer) : base(pointer, typeof(IsCrimson)) { }
        public IsCrimson() : base(N.Conditions.IsCrimson.New().Pointer, typeof(IsCrimson)) { }

    }
    public class IsCrimsonAndNotExpert : NativeDropCondition
    {
        internal IsCrimsonAndNotExpert(IntPtr pointer) : base(pointer, typeof(IsCrimsonAndNotExpert)) { }
        public IsCrimsonAndNotExpert() : base(N.Conditions.IsCrimsonAndNotExpert.New().Pointer, typeof(IsCrimsonAndNotExpert)) { }

    }
    public class IsExpert : NativeDropCondition
    {
        internal IsExpert(IntPtr pointer) : base(pointer, typeof(IsExpert)) { }
        public IsExpert() : base(N.Conditions.IsExpert.New().Pointer, typeof(IsExpert)) { }

    }
    public class IsHardmode : NativeDropCondition
    {
        internal IsHardmode(IntPtr pointer) : base(pointer, typeof(IsHardmode)) { }
        public IsHardmode() : base(N.Conditions.IsHardmode.New().Pointer, typeof(IsHardmode)) { }

    }
    public class IsMasterMode : NativeDropCondition
    {
        internal IsMasterMode(IntPtr pointer) : base(pointer, typeof(IsMasterMode)) { }
        public IsMasterMode() : base(N.Conditions.IsMasterMode.New().Pointer, typeof(IsMasterMode)) { }

    }
    public class IsPumpkinMoon : NativeDropCondition
    {
        internal IsPumpkinMoon(IntPtr pointer) : base(pointer, typeof(IsPumpkinMoon)) { }
        public IsPumpkinMoon() : base(N.Conditions.IsPumpkinMoon.New().Pointer, typeof(IsPumpkinMoon)) { }

    }
    public class IsUsingSpecificAIValues : NativeDropCondition
    {
        internal IsUsingSpecificAIValues(IntPtr pointer) : base(pointer, typeof(IsUsingSpecificAIValues)) { }
        public IsUsingSpecificAIValues(int aislot, float valueToMatch) : base(N.Conditions.IsUsingSpecificAIValues.New(aislot, valueToMatch).Pointer, typeof(IsUsingSpecificAIValues)) { }
        public ref int aiSlotToCheck => ref new N.Conditions.IsUsingSpecificAIValues(Prototype).aiSlotToCheck;
        public ref float valueToMatch => ref new N.Conditions.IsUsingSpecificAIValues(Prototype).valueToMatch;
    }
    public class JungleKeyCondition : NativeDropCondition
    {
        internal JungleKeyCondition(IntPtr pointer) : base(pointer, typeof(JungleKeyCondition)) { }
        public JungleKeyCondition() : base(N.Conditions.JungleKeyCondition.New().Pointer, typeof(JungleKeyCondition)) { }

    }
    public class LegacyHack_IsABoss : NativeDropCondition
    {
        internal LegacyHack_IsABoss(IntPtr pointer) : base(pointer, typeof(LegacyHack_IsABoss)) { }
        public LegacyHack_IsABoss() : base(N.Conditions.LegacyHack_IsABoss.New().Pointer, typeof(LegacyHack_IsABoss)) { }

    }
    public class LegacyHack_IsBossAndExpert : NativeDropCondition
    {
        internal LegacyHack_IsBossAndExpert(IntPtr pointer) : base(pointer, typeof(LegacyHack_IsBossAndExpert)) { }
        public LegacyHack_IsBossAndExpert() : base(N.Conditions.LegacyHack_IsBossAndExpert.New().Pointer, typeof(LegacyHack_IsBossAndExpert)) { }

    }
    public class LegacyHack_IsBossAndNotExpert : NativeDropCondition
    {
        internal LegacyHack_IsBossAndNotExpert(IntPtr pointer) : base(pointer, typeof(LegacyHack_IsBossAndNotExpert)) { }
        public LegacyHack_IsBossAndNotExpert() : base(N.Conditions.LegacyHack_IsBossAndNotExpert.New().Pointer, typeof(LegacyHack_IsBossAndNotExpert)) { }

    }
    public class LivingFlames : NativeDropCondition
    {
        internal LivingFlames(IntPtr pointer) : base(pointer, typeof(LivingFlames)) { }
        public LivingFlames() : base(N.Conditions.LivingFlames.New().Pointer, typeof(LivingFlames)) { }

    }
    public class MechanicalBossesDummyCondition : NativeDropCondition
    {
        internal MechanicalBossesDummyCondition(IntPtr pointer) : base(pointer, typeof(MechanicalBossesDummyCondition)) { }
        public MechanicalBossesDummyCondition() : base(N.Conditions.MechanicalBossesDummyCondition.New().Pointer, typeof(MechanicalBossesDummyCondition)) { }

    }
    public class MechdusaKill : NativeDropCondition
    {
        internal MechdusaKill(IntPtr pointer) : base(pointer, typeof(MechdusaKill)) { }
        public MechdusaKill() : base(N.Conditions.MechdusaKill.New().Pointer, typeof(MechdusaKill)) { }

    }
    public class MissingTwin : NativeDropCondition
    {
        internal MissingTwin(IntPtr pointer) : base(pointer, typeof(MissingTwin)) { }
        public MissingTwin() : base(N.Conditions.MissingTwin.New().Pointer, typeof(MissingTwin)) { }

    }
    public class NamedNPC : NativeDropCondition
    {
        public string neededName { get => new N.Conditions.NamedNPC(Prototype).neededName ?? ""; set => new N.Conditions.NamedNPC(Prototype).neededName = value; }
        internal NamedNPC(IntPtr pointer) : base(pointer, typeof(NamedNPC)) { }
        public NamedNPC(string? neededName) : base(N.Conditions.NamedNPC.New(neededName).Pointer, typeof(NamedNPC)) { }

    }
    public class NeverTrue : NativeDropCondition
    {
        internal NeverTrue(IntPtr pointer) : base(pointer, typeof(NeverTrue)) { }
        public NeverTrue() : base(N.Conditions.NeverTrue.New().Pointer, typeof(NeverTrue)) { }

    }
    public class NotDropExtraGel : NativeDropCondition
    {
        internal NotDropExtraGel(IntPtr pointer) : base(pointer, typeof(NotDropExtraGel)) { }
        public NotDropExtraGel() : base(N.Conditions.NotDropExtraGel.New().Pointer, typeof(NotDropExtraGel)) { }

    }
    public class NotExpert : NativeDropCondition
    {
        internal NotExpert(IntPtr pointer) : base(pointer, typeof(NotExpert)) { }
        public NotExpert() : base(N.Conditions.NotExpert.New().Pointer, typeof(NotExpert)) { }

    }
    public class NotFromStatue : NativeDropCondition
    {
        internal NotFromStatue(IntPtr pointer) : base(pointer, typeof(NotFromStatue)) { }
        public NotFromStatue() : base(N.Conditions.NotFromStatue.New().Pointer, typeof(NotFromStatue)) { }

    }
    public class NotMasterMode : NativeDropCondition
    {
        internal NotMasterMode(IntPtr pointer) : base(pointer, typeof(NotMasterMode)) { }
        public NotMasterMode() : base(N.Conditions.NotMasterMode.New().Pointer, typeof(NotMasterMode)) { }

    }
    public class NotRemixSeed : NativeDropCondition
    {
        internal NotRemixSeed(IntPtr pointer) : base(pointer, typeof(NotRemixSeed)) { }
        public NotRemixSeed() : base(N.Conditions.NotRemixSeed.New().Pointer, typeof(NotRemixSeed)) { }

    }
    public class NotRemixSeedEasymode : NativeDropCondition
    {
        internal NotRemixSeedEasymode(IntPtr pointer) : base(pointer, typeof(NotRemixSeedEasymode)) { }
        public NotRemixSeedEasymode() : base(N.Conditions.NotRemixSeedEasymode.New().Pointer, typeof(NotRemixSeedEasymode)) { }

    }
    public class NotRemixSeedHardmode : NativeDropCondition
    {
        internal NotRemixSeedHardmode(IntPtr pointer) : base(pointer, typeof(NotRemixSeedHardmode)) { }
        public NotRemixSeedHardmode() : base(N.Conditions.NotRemixSeedHardmode.New().Pointer, typeof(NotRemixSeedHardmode)) { }

    }
    public class PirateMap : NativeDropCondition
    {
        internal PirateMap(IntPtr pointer) : base(pointer, typeof(PirateMap)) { }
        public PirateMap() : base(N.Conditions.PirateMap.New().Pointer, typeof(PirateMap)) { }

    }
    public class PlayerNeedsHealing : NativeDropCondition
    {
        internal PlayerNeedsHealing(IntPtr pointer) : base(pointer, typeof(PlayerNeedsHealing)) { }
        public PlayerNeedsHealing() : base(N.Conditions.PlayerNeedsHealing.New().Pointer, typeof(PlayerNeedsHealing)) { }

    }
    public class PumpkinMoonDropGateForTrophies : NativeDropCondition
    {
        internal PumpkinMoonDropGateForTrophies(IntPtr pointer) : base(pointer, typeof(PumpkinMoonDropGateForTrophies)) { }
        public PumpkinMoonDropGateForTrophies() : base(N.Conditions.PumpkinMoonDropGateForTrophies.New().Pointer, typeof(PumpkinMoonDropGateForTrophies)) { }

    }
    public class PumpkinMoonDropGatingChance : NativeDropCondition
    {
        internal PumpkinMoonDropGatingChance(IntPtr pointer) : base(pointer, typeof(PumpkinMoonDropGatingChance)) { }
        public PumpkinMoonDropGatingChance() : base(N.Conditions.PumpkinMoonDropGatingChance.New().Pointer, typeof(PumpkinMoonDropGatingChance)) { }

    }
    public class RedHatSkeletron : NativeDropCondition
    {
        internal RedHatSkeletron(IntPtr pointer) : base(pointer, typeof(RedHatSkeletron)) { }
        public RedHatSkeletron() : base(N.Conditions.RedHatSkeletron.New().Pointer, typeof(RedHatSkeletron)) { }

    }
    public class RemixSeed : NativeDropCondition
    {
        internal RemixSeed(IntPtr pointer) : base(pointer, typeof(RemixSeed)) { }
        public RemixSeed() : base(N.Conditions.RemixSeed.New().Pointer, typeof(RemixSeed)) { }

    }
    public class RemixSeedEasymode : NativeDropCondition
    {
        internal RemixSeedEasymode(IntPtr pointer) : base(pointer, typeof(RemixSeedEasymode)) { }
        public RemixSeedEasymode() : base(N.Conditions.RemixSeedEasymode.New().Pointer, typeof(RemixSeedEasymode)) { }

    }
    public class RemixSeedHardmode : NativeDropCondition
    {
        internal RemixSeedHardmode(IntPtr pointer) : base(pointer, typeof(RemixSeedHardmode)) { }
        public RemixSeedHardmode() : base(N.Conditions.RemixSeedHardmode.New().Pointer, typeof(RemixSeedHardmode)) { }

    }
    public class SkyblockIsNotUp : NativeDropCondition
    {
        internal SkyblockIsNotUp(IntPtr pointer) : base(pointer, typeof(SkyblockIsNotUp)) { }
        public SkyblockIsNotUp() : base(N.Conditions.SkyblockIsNotUp.New().Pointer, typeof(SkyblockIsNotUp)) { }

    }
    public class SkyblockIsUp : NativeDropCondition
    {
        internal SkyblockIsUp(IntPtr pointer) : base(pointer, typeof(SkyblockIsUp)) { }
        public SkyblockIsUp() : base(N.Conditions.SkyblockIsUp.New().Pointer, typeof(SkyblockIsUp)) { }

    }
    public class SkyblockIsUpNoSickle : NativeDropCondition
    {
        internal SkyblockIsUpNoSickle(IntPtr pointer) : base(pointer, typeof(SkyblockIsUpNoSickle)) { }
        public SkyblockIsUpNoSickle() : base(N.Conditions.SkyblockIsUpNoSickle.New().Pointer, typeof(SkyblockIsUpNoSickle)) { }

    }
    public class SoulOfLight : NativeDropCondition
    {
        internal SoulOfLight(IntPtr pointer) : base(pointer, typeof(SoulOfLight)) { }
        public SoulOfLight() : base(N.Conditions.SoulOfLight.New().Pointer, typeof(SoulOfLight)) { }

    }
    public class SoulOfNight : NativeDropCondition
    {
        internal SoulOfNight(IntPtr pointer) : base(pointer, typeof(SoulOfNight)) { }
        public SoulOfNight() : base(N.Conditions.SoulOfNight.New().Pointer, typeof(SoulOfNight)) { }

    }
    public class TenthAnniversaryIsNotUp : NativeDropCondition
    {
        internal TenthAnniversaryIsNotUp(IntPtr pointer) : base(pointer, typeof(TenthAnniversaryIsNotUp)) { }
        public TenthAnniversaryIsNotUp() : base(N.Conditions.TenthAnniversaryIsNotUp.New().Pointer, typeof(TenthAnniversaryIsNotUp)) { }

    }
    public class TenthAnniversaryIsUp : NativeDropCondition
    {
        internal TenthAnniversaryIsUp(IntPtr pointer) : base(pointer, typeof(TenthAnniversaryIsUp)) { }
        public TenthAnniversaryIsUp() : base(N.Conditions.TenthAnniversaryIsUp.New().Pointer, typeof(TenthAnniversaryIsUp)) { }

    }
    public class WindyEnoughForKiteDrops : NativeDropCondition
    {
        internal WindyEnoughForKiteDrops(IntPtr pointer) : base(pointer, typeof(WindyEnoughForKiteDrops)) { }
        public WindyEnoughForKiteDrops() : base(N.Conditions.WindyEnoughForKiteDrops.New().Pointer, typeof(WindyEnoughForKiteDrops)) { }

    }
    public class XmasPresentDrop : NativeDropCondition
    {
        internal XmasPresentDrop(IntPtr pointer) : base(pointer, typeof(XmasPresentDrop)) { }
        public XmasPresentDrop() : base(N.Conditions.XmasPresentDrop.New().Pointer, typeof(XmasPresentDrop)) { }

    }
    public class YoyoCascade : NativeDropCondition
    {
        internal YoyoCascade(IntPtr pointer) : base(pointer, typeof(YoyoCascade)) { }
        public YoyoCascade() : base(N.Conditions.YoyoCascade.New().Pointer, typeof(YoyoCascade)) { }

    }
    public class YoyosAmarok : NativeDropCondition
    {
        internal YoyosAmarok(IntPtr pointer) : base(pointer, typeof(YoyosAmarok)) { }
        public YoyosAmarok() : base(N.Conditions.YoyosAmarok.New().Pointer, typeof(YoyosAmarok)) { }

    }
    public class YoyosHelFire : NativeDropCondition
    {
        internal YoyosHelFire(IntPtr pointer) : base(pointer, typeof(YoyosHelFire)) { }
        public YoyosHelFire() : base(N.Conditions.YoyosHelFire.New().Pointer, typeof(YoyosHelFire)) { }

    }
    public class YoyosKraken : NativeDropCondition
    {
        internal YoyosKraken(IntPtr pointer) : base(pointer, typeof(YoyosKraken)) { }
        public YoyosKraken() : base(N.Conditions.YoyosKraken.New().Pointer, typeof(YoyosKraken)) { }

    }
    public class YoyosYelets : NativeDropCondition
    {
        internal YoyosYelets(IntPtr pointer) : base(pointer, typeof(YoyosYelets)) { }
        public YoyosYelets() : base(N.Conditions.YoyosYelets.New().Pointer, typeof(YoyosYelets)) { }

    }
}
