using System.Collections.Generic;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModPrefix : ModType, ILocalizedModType
{
    public int Type { get; internal set; }

    public virtual string LocalizationCategory => "Prefixes";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual PrefixCategory Category => PrefixCategory.Custom;

    public virtual float RollChance(Item item) => 1f;

    public virtual bool CanRoll(Item item) => true;

    public virtual void SetStats(ref float damageMult, ref float knockbackMult, ref float useTimeMult, ref float scaleMult,
                                 ref float shootSpeedMult, ref float manaMult, ref int critBonus) { }

    public virtual bool AllStatChangesHaveEffectOn(Item item) => true;

    public virtual void Apply(Item item) { }

    public virtual void ModifyValue(ref float valueMult) { }

    public virtual void ApplyAccessoryEffects(Player player) { }

    public virtual IEnumerable<TooltipLine>? GetTooltipLines(Item item) => null;

    public sealed override void SetupContent() => SetStaticDefaults();

    protected sealed override void Register() => PrefixLoader.Register(this);
}
