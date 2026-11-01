using System;
using Terraria.IO;
using Terraria.WorldBuilding;
using NativeLegacy = BunnyLoader.Interop.WorldGen.PassLegacy;

namespace Terraria.GameContent.Generation;

public delegate void WorldGenLegacyMethod(GenerationProgress progress, GameConfiguration configuration);

public class PassLegacy : GenPass
{
    readonly WorldGenLegacyMethod method;

    public PassLegacy(string name, WorldGenLegacyMethod method) :
        this(name, method, DefaultWeight(name))
    {
    }

    public PassLegacy(string name, WorldGenLegacyMethod method, double weight) : base(name, weight)
    {
        ArgumentNullException.ThrowIfNull(method);
        this.method = method;
    }

    static double DefaultWeight(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return NativeLegacy.GetWeight(name);
    }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        method(progress, configuration);
    }
}
