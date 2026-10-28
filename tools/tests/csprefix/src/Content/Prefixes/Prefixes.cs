using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsPrefix.Content.Prefixes;

public class Shiny : ModPrefix
{
    public static int Applied;

    public override PrefixCategory Category => PrefixCategory.Melee;

    public override void SetStats(ref float damageMult, ref float knockbackMult, ref float useTimeMult, ref float scaleMult,
                                  ref float shootSpeedMult, ref float manaMult, ref int critBonus)
    {
        damageMult *= 1.2f;
        critBonus += 5;
    }

    public override void ModifyValue(ref float valueMult) => valueMult *= 1.1f;

    public override void Apply(Item item) => Applied++;

    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        yield return new TooltipLine(Mod, "Shine", "Shines") { IsModifier = true };
    }
}

public class Lucky : ModPrefix
{
    public static int Effects;

    public override PrefixCategory Category => PrefixCategory.Accessory;

    public override void ApplyAccessoryEffects(Player player) => Effects++;
}
