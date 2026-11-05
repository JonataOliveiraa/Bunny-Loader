using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.BigProgressBar;

namespace Terraria.ModLoader;

public abstract class ModBossBarStyle : ModType
{
    public bool IsSelected => BossBarLoader.CurrentStyle == this;

    public virtual string DisplayName => Mod.DisplayName;

    public virtual bool PreventUpdate => false;

    public virtual bool PreventDraw => false;

    protected sealed override void Register() => BossBarLoader.AddBossBarStyle(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void Update(IBigProgressBar currentBar, ref BigProgressBarInfo info) { }

    public virtual void OnSelected() { }

    public virtual void OnDeselected() { }

    public virtual void Draw(SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) { }
}
