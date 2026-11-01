namespace Terraria.ModLoader;

[Autoload(Side = ModSide.Client)]
public abstract class ModWaterfallStyle : ModTexturedType
{
    public int Slot { get; internal set; }

    protected sealed override void Register() => Slot = WaterFallStylesLoader.Register(this);

    public sealed override void SetupContent()
    {
        WaterFallStylesLoader.SetupTexture(this);
        SetStaticDefaults();
    }

    public virtual void AddLight(int i, int j) { }

    public virtual void ColorMultiplier(ref float r, ref float g, ref float b, float a) { }
}
