using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

public abstract class ModRarity : ModType
{
    public int Type { get; internal set; }

    public virtual Color RarityColor => Color.White;

    public virtual int GetPrefixedRarity(int offset, float valueMult) => Type;

    public sealed override void SetupContent() => SetStaticDefaults();

    protected sealed override void Register() => RarityLoader.Register(this);
}
