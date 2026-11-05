using System.Collections.Generic;
using Terraria.Map;

namespace Terraria.ModLoader;

public abstract class ModMapLayer : ModType
{
    public bool Visible { get; set; } = true;

    public virtual Position GetDefaultPosition() => AfterLastVanillaLayer;

    public virtual IEnumerable<Position>? GetModdedConstraints() => null;

    public abstract void Draw(ref MapOverlayDrawContext context, ref string text);

    protected sealed override void Register()
    {
        ModTypeLookup<ModMapLayer>.Register(this);
        MapLayerLoader.Add(this);
    }

    public sealed override void SetupContent() => SetStaticDefaults();

    public static Position BeforeFirstVanillaLayer => new Before(IMapLayer.Digtoise);

    public static Position AfterLastVanillaLayer => new After(IMapLayer.Pings);

    public abstract class Position
    {
        internal IMapLayer Vanilla;
        internal ModMapLayer? Modded;
    }

    public sealed class Before : Position
    {
        public Before(IMapLayer layer) => Vanilla = layer;
        public Before(ModMapLayer layer) => Modded = layer;
    }

    public sealed class After : Position
    {
        public After(IMapLayer layer) => Vanilla = layer;
        public After(ModMapLayer layer) => Modded = layer;
    }
}
