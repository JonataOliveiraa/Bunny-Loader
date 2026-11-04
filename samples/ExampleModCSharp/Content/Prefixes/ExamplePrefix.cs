using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Prefixes;

public class ExamplePrefix : ModPrefix
{
    public virtual float Power => 1f;

    public override PrefixCategory Category => PrefixCategory.AnyWeapon;

    public override float RollChance(Item item) => 5f;

    public override bool CanRoll(Item item) => true;

    public override void SetStats(ref float damageMult, ref float knockbackMult, ref float useTimeMult, ref float scaleMult, ref float shootSpeedMult, ref float manaMult, ref int critBonus)
    {
        damageMult *= 1f + 0.20f * Power;
    }

    public override void ModifyValue(ref float valueMult)
    {
        valueMult *= 1f + 0.05f * Power;
    }

    public override void Apply(Item item)
    {
    }

    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        yield return new TooltipLine(Mod, "PrefixWeaponAwesome", PowerTooltip.Format(Power))
        {
            Color = Terraria.ID.Colors.PrefixGood,
        };

        yield return new TooltipLine(Mod, "PrefixWeaponAwesomeDescription", AdditionalTooltip.Value)
        {
            Color = Terraria.ID.Colors.PrefixGood,
        };
    }

    public static LocalizedText PowerTooltip { get; private set; }

    public LocalizedText AdditionalTooltip => this.GetLocalization(nameof(AdditionalTooltip));

    public override void SetStaticDefaults()
    {
        PowerTooltip = Mod.GetLocalization($"{LocalizationCategory}.{nameof(PowerTooltip)}");
        _ = AdditionalTooltip;
    }
}
