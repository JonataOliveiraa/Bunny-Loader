using System.Collections.Generic;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

[Autoload(Side = ModSide.Client)]
public abstract class ModCloud : ModTexturedType
{
    internal string? nameOverride;
    internal string? textureOverride;
    internal float spawnChance = 1f;
    internal bool rareCloud;

    public int Type { get; internal set; }

    public virtual bool RareCloud => rareCloud;

    public override string Name => nameOverride ?? base.Name;

    public override string Texture => textureOverride ?? base.Texture;

    protected override void Register() => CloudLoader.RegisterModCloud(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual float SpawnChance() => spawnChance;

    public virtual void OnSpawn(Cloud cloud) { }

    public virtual bool Draw(List<DrawData> drawDataCache, Cloud cloud, int cloudIndex, ref DrawData drawData) => true;
}

[Autoload(false)]
public sealed class SimpleModCloud : ModCloud
{
}
