using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class DamageClass : ModType, ILocalizedModType
{
    public static DamageClass Default => DamageClassLoader.Vanilla.Default;
    public static DamageClass Generic => DamageClassLoader.Vanilla.Generic;
    public static DamageClass Melee => DamageClassLoader.Vanilla.Melee;
    public static DamageClass MeleeNoSpeed => DamageClassLoader.Vanilla.MeleeNoSpeed;
    public static DamageClass Ranged => DamageClassLoader.Vanilla.Ranged;
    public static DamageClass Magic => DamageClassLoader.Vanilla.Magic;
    public static DamageClass Summon => DamageClassLoader.Vanilla.Summon;
    public static DamageClass SummonMeleeSpeed => DamageClassLoader.Vanilla.SummonMeleeSpeed;
    public static DamageClass MagicSummonHybrid => DamageClassLoader.Vanilla.MagicSummonHybrid;
    public static DamageClass Throwing => DamageClassLoader.Vanilla.Throwing;

    public int Type { get; internal set; }

    public virtual string LocalizationCategory => "DamageClasses";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic ? StatInheritanceData.Full : StatInheritanceData.None;

    public virtual bool GetEffectInheritance(DamageClass damageClass) => false;

    public virtual bool GetPrefixInheritance(DamageClass damageClass) => GetEffectInheritance(damageClass);

    public virtual void SetDefaultStats(Player player) { }

    public virtual bool UseStandardCritCalcs => true;

    public virtual bool ShowStatTooltipLine(Player player, string lineName) => true;

    public bool CountsAsClass(DamageClass damageClass) => DamageClassLoader.CountsAs(this, damageClass);

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool GetsPrefixesFor(DamageClass damageClass) =>
        this == damageClass || DamageClassLoader.Safe(this, nameof(GetPrefixInheritance), () => GetPrefixInheritance(damageClass), false);

    public sealed override void SetupContent() => SetStaticDefaults();

    protected sealed override void Register() => DamageClassLoader.Register(this);

    internal virtual int VanillaTip => -1;

    internal bool IsVanilla => VanillaTip >= 0;
}
