using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class InfoDisplay : ModTexturedType, ILocalizedModType
{
    public virtual string LocalizationCategory => "InfoDisplays";
    public virtual string HoverTexture => Texture;
    public static Color InactiveInfoTextColor => new(100, 100, 100);
    public virtual bool Active() => true;
    public abstract string DisplayValue(ref Color displayColor, ref Color displayShadowColor);
    protected sealed override void Register() { ModTypeLookup<InfoDisplay>.Register(this); InfoDisplayHooks.Register(this); }
    public sealed override void SetupContent() { ModContent.Request<Texture2D>(Texture); ModContent.Request<Texture2D>(HoverTexture); SetStaticDefaults(); }
}
