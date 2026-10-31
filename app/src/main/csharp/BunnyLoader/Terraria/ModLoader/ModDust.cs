using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria.ModLoader;

public abstract unsafe class ModDust : ModTexturedType
{
    int updateType = -1;

    public int UpdateType
    {
        get => updateType;
        set
        {
            updateType = value;
            if (Type > 0) Host.Native()->SetDustUpdateType(Type, value);
        }
    }

    public Asset<Texture2D> Texture2D => DustLoader.TextureOf(this);

    public int Type { get; internal set; }

    protected sealed override void Register() => DustLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public override void SetStaticDefaults() { }

    public virtual bool PreDraw(Dust dust) => true;

    public virtual void OnSpawn(Dust dust) { }

    public virtual bool Update(Dust dust) => true;

    public virtual bool MidUpdate(Dust dust) => false;

    public virtual Color? GetAlpha(Dust dust, Color lightColor) => null;
}
