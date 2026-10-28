using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CsRarity.Content.Rarities;

public class Teal : ModRarity
{
    public static readonly Color Shade = new(0, 200, 200);

    public static bool SetUp;

    public override Color RarityColor => Shade;

    public override void SetStaticDefaults() => SetUp = true;

    public override int GetPrefixedRarity(int offset, float valueMult) => offset < 0 ? 1 : Type;
}
