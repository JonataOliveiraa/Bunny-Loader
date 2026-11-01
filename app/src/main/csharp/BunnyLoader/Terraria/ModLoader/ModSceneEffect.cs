using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics.Capture;

namespace Terraria.ModLoader;

public abstract class ModSceneEffect : ModType
{
    public int Type { get; internal set; }

    public virtual ModWaterStyle? WaterStyle => null;

    public virtual ModSurfaceBackgroundStyle? SurfaceBackgroundStyle => null;

    public virtual ModUndergroundBackgroundStyle? UndergroundBackgroundStyle => null;

    public virtual int Music => -1;

    public virtual string? MapBackground => null;

    public virtual bool MapBackgroundFullbright => false;

    public virtual SceneEffectPriority Priority => SceneEffectPriority.None;

    public virtual CaptureBiome.TileColorStyle TileColorStyle => CaptureBiome.TileColorStyle.Normal;

    protected override void Register() => Type = SceneEffectLoader.Register(this);

    public override void SetupContent() => SetStaticDefaults();

    public virtual float GetWeight(Player player) => 0.5f;

    internal float GetCorrWeight(Player player) => Math.Max(Math.Min(GetWeight(player), 1), 0) + (float)Priority;

    public virtual bool IsSceneEffectActive(Player player) => false;

    public virtual void SpecialVisuals(Player player, bool isActive) { }

    public virtual void MapBackgroundColor(ref Color color) { }
}
