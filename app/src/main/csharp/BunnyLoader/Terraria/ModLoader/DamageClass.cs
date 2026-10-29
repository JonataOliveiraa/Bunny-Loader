using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class DamageClass : ModType, ILocalizedModType
{
    public static DamageClass Default { get; private set; } = new DefaultDamageClass();

    public static DamageClass Generic { get; private set; } = new GenericDamageClass();

    public static DamageClass Melee { get; private set; } = new MeleeDamageClass();

    public static DamageClass MeleeNoSpeed { get; private set; } = new MeleeNoSpeedDamageClass();

    public static DamageClass Ranged { get; private set; } = new RangedDamageClass();

    public static DamageClass Magic { get; private set; } = new MagicDamageClass();

    public static DamageClass Summon { get; private set; } = new SummonDamageClass();

    public static DamageClass SummonMeleeSpeed { get; private set; } = new SummonMeleeSpeedDamageClass();

    public static DamageClass MagicSummonHybrid { get; private set; } = new MagicSummonHybridDamageClass();

    public static DamageClass Throwing { get; private set; } = new ThrowingDamageClass();

    static DamageClass()
    {
        DamageClass[] vanilla = { Default, Generic, Melee, MeleeNoSpeed, Ranged, Magic, Summon, SummonMeleeSpeed, MagicSummonHybrid, Throwing };
        for (int i = 0; i < vanilla.Length; i++) vanilla[i].Type = i;
    }

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

    protected sealed override void Register() => Type = DamageClassLoader.Add(this);

    public sealed override void SetupContent()
    {
        _ = DisplayName;
        SetStaticDefaults();
    }

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool CountsAsClass(DamageClass damageClass) => DamageClassLoader.CountsAs(this, damageClass);

    public bool GetsPrefixesFor<T>() where T : DamageClass => GetsPrefixesFor(ModContent.GetInstance<T>());

    public bool GetsPrefixesFor(DamageClass damageClass) => this == damageClass || GetPrefixInheritance(damageClass);
}
