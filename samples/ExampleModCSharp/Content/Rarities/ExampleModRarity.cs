using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace ExampleMod.Content.Rarities;

public class ExampleModRarity : ModRarity
{
    public override Color RarityColor => new Color(200, 215, 230);

    public override int GetPrefixedRarity(int offset, float valueMult)
    {
        if (offset > 0)
        {
            return ModContent.RarityType<ExampleHigherTierModRarity>();
        }

        return Type;
    }
}
