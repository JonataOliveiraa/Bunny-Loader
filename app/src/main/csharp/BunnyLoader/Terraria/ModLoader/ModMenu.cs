using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ID;

namespace Terraria.ModLoader;

public abstract class ModMenu : ModType
{
    protected sealed override void Register() => MenuLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual Asset<Texture2D>? Logo => null;

    public virtual Asset<Texture2D>? SunTexture => null;

    public virtual Asset<Texture2D>? MoonTexture => null;

    public virtual int Music => MusicID.TitleJourneysBeginningWithIntro;

    public virtual ModSurfaceBackgroundStyle? MenuBackgroundStyle => null;

    public virtual bool IsAvailable => true;

    public virtual string DisplayName => Mod?.DisplayName ?? Name;

    public bool IsSelected => MenuLoader.CurrentMenu == this;

    public virtual void OnSelected() { }

    public virtual void OnDeselected() { }

    public virtual void Update(bool isOnTitleScreen) { }

    public virtual bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor) => true;

    public virtual void PostDrawLogo(SpriteBatch spriteBatch, Vector2 logoDrawCenter, float logoRotation, float logoScale, Color drawColor) { }
}

[Autoload(false)]
internal sealed class VanillaMenu : ModMenu
{
    public override string DisplayName => "Terraria";
}
