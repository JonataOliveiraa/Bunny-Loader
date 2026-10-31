using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.ResourceSets;

namespace Terraria.ModLoader;

public abstract class ModResourceOverlay : ModType
{
    public int Type { get; internal set; }

    protected sealed override void Register() => Type = ResourceOverlayLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual bool PreDrawResource(ResourceOverlayDrawContext context) => true;

    public virtual void PostDrawResource(ResourceOverlayDrawContext context) { }

    public virtual bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
    {
        drawText = true;
        return true;
    }

    public virtual void PostDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, Color textColor, bool drawText) { }

    public virtual bool DisplayHoverText(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife) => true;
}

public struct ResourceOverlayDrawContext
{
    public readonly PlayerStatsSnapshot snapshot;
    public readonly int resourceNumber;
    public ReLogic.Content.Asset<Texture2D> texture;
    public Vector2 position;
    public Rectangle? source;
    public Color color;
    public float rotation;
    public Vector2 origin;
    public Vector2 scale;
    public SpriteEffects effects;

    public IPlayerResourcesDisplaySet DisplaySet { get; init; }

    public SpriteBatch SpriteBatch { get; init; }

    public ResourceOverlayDrawContext(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, int resourceNumber, ReLogic.Content.Asset<Texture2D> texture)
    {
        this.snapshot = snapshot;
        DisplaySet = displaySet;
        this.resourceNumber = resourceNumber;
        this.texture = texture;
        position = Vector2.Zero;
        source = null;
        color = Color.White;
        rotation = 0;
        origin = Vector2.Zero;
        scale = Vector2.One;
        effects = SpriteEffects.None;
        SpriteBatch = Main.spriteBatch;
    }

    public void Draw() => SpriteBatch.Draw(texture.Value, position, source, color, rotation, origin, scale, effects, 0);
}
