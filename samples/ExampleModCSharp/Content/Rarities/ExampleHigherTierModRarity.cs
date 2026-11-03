using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Rarities;

public class ExampleHigherTierModRarity : ModRarity
{
    public override Color RarityColor => new Color(Main.DiscoR / 2, (byte)(Main.DiscoG / 1.25f), (byte)(Main.DiscoB / 1.5f));

    public override int GetPrefixedRarity(int offset, float valueMult)
    {
        if (offset < 0)
        {
            return ModContent.RarityType<ExampleModRarity>();
        }

        return Type;
    }
}
