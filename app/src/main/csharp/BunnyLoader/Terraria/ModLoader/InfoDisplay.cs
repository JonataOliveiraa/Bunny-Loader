using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class InfoDisplay : ModTexturedType, ILocalizedModType
{
    public static Color InactiveInfoTextColor => new(100, 100, 100, Main.mouseTextColor);
    public static Color GoldInfoTextColor => new(Main.OurFavoriteColor.R, Main.OurFavoriteColor.G, Main.OurFavoriteColor.B, Main.mouseTextColor);
    public static Color GoldInfoTextShadowColor => new(Main.OurFavoriteColor.R / 10, Main.OurFavoriteColor.G / 10, Main.OurFavoriteColor.B / 10, Main.mouseTextColor);
    public static string VanillaHoverTexture => "Terraria/Images/UI/InfoIcon_13";
    public virtual string LocalizationCategory => "InfoDisplays";
    public virtual string HoverTexture => VanillaHoverTexture;
    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);
    public virtual bool Active() => false;
    public abstract string DisplayValue(ref Color displayColor, ref Color displayShadowColor);
    protected sealed override void Register() { ModTypeLookup<InfoDisplay>.Register(this); InfoDisplayHooks.Register(this); }
    public sealed override void SetupContent() { _ = DisplayName; ModContent.Request<Texture2D>(Texture); SetStaticDefaults(); }
}
