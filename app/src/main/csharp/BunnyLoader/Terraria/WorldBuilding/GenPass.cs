using System;
using BunnyLoader.Interop;
using Terraria.IO;
using Terraria.ModLoader.Core;
using Terraria.Utilities;
using NativePass = BunnyLoader.Interop.WorldGen.GenPass;
using NativeLegacy = BunnyLoader.Interop.WorldGen.PassLegacy;

namespace Terraria.WorldBuilding;

public abstract class GenBase
{
    protected static UnifiedRandom _random => WorldGen.genRand;

    protected static TileData _tiles => Main.tile;
}

public abstract class GenPass : GenBase, IGameObject
{
    readonly GameHandle handle;

    internal NativePass Native => new(handle.Target);

    public IntPtr Pointer => handle.Target;

    public string Name
    {
        get => Native.Name ?? "";
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            NativePass pass = Native;
            pass.Name = value;
        }
    }

    public double Weight
    {
        get => Native.Weight;
        set
        {
            ValidateWeight(value);
            NativePass pass = Native;
            pass.Weight = value;
        }
    }

    public bool Enabled => Native.Enabled;

    public GenPass(string name, double loadWeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateWeight(loadWeight);
        NativeLegacy pass = NativeLegacy.New(name, default, loadWeight);
        handle = new GameHandle(pass.Pointer);
        WorldGenHooks.Track(this);
    }

    internal GenPass(NativePass native)
    {
        if (native.IsNull)
            throw new ArgumentNullException(nameof(native));
        handle = new GameHandle(native.Pointer);
    }

    static void ValidateWeight(double weight)
    {
        if (!double.IsFinite(weight) || weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Generation pass weight must be finite and non-negative");
    }

    public void Disable() => Native.Disable();

    public void Enable() => Native.Enable();

    public void Apply(GenerationProgress progress, GameConfiguration configuration)
    {
        try
        {
            Native.Apply(progress, configuration);
        }
        finally
        {
            GC.KeepAlive(this);
        }
    }

    protected abstract void ApplyPass(GenerationProgress progress, GameConfiguration configuration);

    internal void Invoke(GenerationProgress progress, GameConfiguration configuration) => ApplyPass(progress, configuration);
}
