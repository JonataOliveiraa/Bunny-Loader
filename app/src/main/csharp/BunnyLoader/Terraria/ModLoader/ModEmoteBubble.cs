using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModEmoteBubble : ModType<EmoteBubble, ModEmoteBubble>, ILocalizedModType
{
    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

    public int Type { get; internal set; }

    public EmoteBubble EmoteBubble => Entity;

    public virtual string LocalizationCategory => "Emotes";

    public virtual LocalizedText Command => this.GetLocalization(nameof(Command), () => Name.ToLower());

    public sealed override void SetupContent() => SetStaticDefaults();

    protected sealed override void Register() => EmoteBubbleLoader.Add(this);

    protected override EmoteBubble CreateTemplateEntity() => default;

    public void AddToCategory(int categoryId) => EmoteBubbleLoader.AddToCategory(this, categoryId);

    public virtual bool IsUnlocked() => true;

    public virtual void OnSpawn() { }

    public virtual bool UpdateFrame() => true;

    public virtual bool UpdateFrameInEmoteMenu(ref int frameCounter) => true;

    public virtual bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects) => true;

    public virtual void PostDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects) { }

    public virtual Rectangle? GetFrame() => null;

    public virtual Rectangle? GetFrameInEmoteMenu(int frame, int frameCounter) => null;
}
