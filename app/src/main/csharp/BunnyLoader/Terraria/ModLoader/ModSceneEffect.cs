using System;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

public abstract class ModSceneEffect : ModType
{
    public int Type { get; internal set; }

    public virtual int Music => -1;

    public virtual string? MapBackground => null;

    public virtual bool MapBackgroundFullbright => false;

    public virtual SceneEffectPriority Priority => SceneEffectPriority.None;

    protected override void Register() => Type = SceneEffectLoader.Register(this);

    public override void SetupContent() => SetStaticDefaults();

    public virtual float GetWeight(Player player) => 0.5f;

    internal float GetCorrWeight(Player player) => Math.Max(Math.Min(GetWeight(player), 1), 0) + (float)Priority;

    public virtual bool IsSceneEffectActive(Player player) => false;

    public virtual void SpecialVisuals(Player player, bool isActive) { }

    public virtual void MapBackgroundColor(ref Color color) { }
}
