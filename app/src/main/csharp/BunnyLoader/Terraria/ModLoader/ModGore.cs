using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class ModGore : ModTexturedType
{
    internal string? nameOverride;
    internal string? textureOverride;

    public int UpdateType { get; set; } = -1;

    public int Type { get; internal set; }

    public override string Name => nameOverride ?? base.Name;

    public override string Texture => textureOverride ?? base.Texture;

    protected override void Register() => GoreLoader.RegisterModGore(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void OnSpawn(Gore gore, IEntitySource source) { }

    public virtual bool Update(Gore gore) => true;

    public virtual Color? GetAlpha(Gore gore, Color lightColor) => null;
}

[Autoload(false)]
public sealed class SimpleModGore : ModGore
{
}
