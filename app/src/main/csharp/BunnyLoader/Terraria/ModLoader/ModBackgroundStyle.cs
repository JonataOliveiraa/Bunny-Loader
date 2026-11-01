using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

public abstract class ModBackgroundStyle : ModType
{
    public int Slot { get; internal set; }
}

public abstract class ModUndergroundBackgroundStyle : ModBackgroundStyle
{
    protected sealed override void Register() => Slot = UndergroundBackgroundStylesLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public abstract void FillTextureArray(int[] textureSlots);
}

public abstract class ModSurfaceBackgroundStyle : ModBackgroundStyle
{
    protected sealed override void Register() => Slot = SurfaceBackgroundStylesLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public abstract void ModifyFarFades(float[] fades, float transitionSpeed);

    public virtual int ChooseFarTexture() => -1;

    public virtual int ChooseMiddleTexture() => -1;

    public virtual bool PreDrawCloseBackground(SpriteBatch spriteBatch) => true;

    public virtual int ChooseCloseTexture(ref float scale, ref double parallax, ref float a, ref float b) => -1;
}

public abstract class GlobalBackgroundStyle : ModType
{
    protected sealed override void Register() => GlobalBackgroundStyleLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void ChooseUndergroundBackgroundStyle(ref int style) { }

    public virtual void ChooseSurfaceBackgroundStyle(ref int style) { }

    public virtual void FillUndergroundTextureArray(int style, int[] textureSlots) { }

    public virtual void ModifyFarSurfaceFades(int style, float[] fades, float transitionSpeed) { }
}
