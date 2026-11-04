using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Prefixes;

public class ExampleAccessoryPrefix : ModPrefix
{
    public static readonly int DefenseBonus = 4;

    public override PrefixCategory Category => PrefixCategory.Accessory;

    public override void ModifyValue(ref float valueMult)
    {
        valueMult *= 1.2f;
    }

    public override void ApplyAccessoryEffects(Player player)
    {
        player.statDefense += DefenseBonus;
    }

    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        yield return new TooltipLine(Mod, "PrefixAccDefense", "+" + DefenseBonus + Lang.tip[25].Value)
        {
            IsModifier = true,
        };
    }
}
