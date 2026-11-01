using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;

namespace Terraria.ModLoader;

[Autoload(Side = ModSide.Client)]
public abstract class ModWaterStyle : ModTexturedType
{
    public int Slot { get; internal set; }

    public virtual string BlockTexture => Texture + "_Block";

    public virtual string SlopeTexture => Texture + "_Slope";

    protected sealed override void Register() => Slot = WaterStylesLoader.Register(this);

    public sealed override void SetupContent()
    {
        WaterStylesLoader.SetupTextures(this);
        SetStaticDefaults();
    }

    public abstract int ChooseWaterfallStyle();

    public abstract int GetSplashDust();

    public abstract int GetDropletGore();

    public virtual void LightColorMultiplier(ref float r, ref float g, ref float b)
    {
        r = 0.88f;
        g = 0.96f;
        b = 1.015f;
    }

    public virtual Color BiomeHairColor() => new(28, 216, 94);

    public virtual Asset<Texture2D> GetRainTexture() => TextureAssets.Rain;

    public virtual byte GetRainVariant() => (byte)Main.rand.Next(3);
}
