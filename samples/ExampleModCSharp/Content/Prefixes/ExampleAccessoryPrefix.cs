using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Prefixes;

// This prefix shows how to make an accessory prefix, like Warding or Armored. It gives 4 defense to the player wearing the accessory.
// See ExamplePrefix for a weapon prefix.
public class ExampleAccessoryPrefix : ModPrefix
{
    public static readonly int DefenseBonus = 4;

    // Accessory prefixes can be rolled at the Goblin Tinkerer and when an accessory is crafted or dropped.
    public override PrefixCategory Category => PrefixCategory.Accessory;

    // Accessories with this prefix sell and buy for 20% more.
    public override void ModifyValue(ref float valueMult)
    {
        valueMult *= 1.2f;
    }

    // ApplyAccessoryEffects runs every tick while an accessory with this prefix is equipped, like ModItem.UpdateAccessory.
    public override void ApplyAccessoryEffects(Player player)
    {
        player.statDefense += DefenseBonus;
    }

    // Adds the bonus to the item's tooltip. Lang.tip[25] is the vanilla " defense" text, so this line reads like the vanilla accessory prefixes.
    // IsModifier colors the line like the other prefix lines.
    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        yield return new TooltipLine(Mod, "PrefixAccDefense", "+" + DefenseBonus + Lang.tip[25].Value)
        {
            IsModifier = true,
        };
    }
}
