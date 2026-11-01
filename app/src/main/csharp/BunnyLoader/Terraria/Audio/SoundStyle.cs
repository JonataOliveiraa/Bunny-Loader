using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ModLoader.Core;

namespace Terraria.Audio;

public readonly partial struct SoundStyle
{
    static readonly Dictionary<int, GameHandle> sounds = new();

    readonly int[]? variants;
    readonly float volume;

    public string? SoundPath { get; init; }

    public SoundType Type { get; init; }

    public string? Identifier { get; init; }

    public float Volume
    {
        get => volume;
        init => volume = Math.Clamp(value, 0f, 1f);
    }

    public float Pitch { get; init; }

    public float PitchVariance { get; init; }

    public (float minPitch, float maxPitch) PitchRange
    {
        get => (Pitch - PitchVariance * 0.5f, Pitch + PitchVariance * 0.5f);
        init
        {
            if (value.maxPitch < value.minPitch) throw new ArgumentException("Min pitch cannot be greater than max pitch.");
            Pitch = (value.minPitch + value.maxPitch) * 0.5f;
            PitchVariance = value.maxPitch - value.minPitch;
        }
    }

    public int MaxInstances { get; init; }

    public SoundLimitBehavior SoundLimitBehavior { get; init; }

    public bool IsLooped { get; init; }

    public bool PlayOnlyIfFocused { get; init; }

    public ReadOnlySpan<int> Variants
    {
        get => variants;
        init => variants = value.IsEmpty ? null : value.ToArray();
    }

    internal LegacySoundStyle Legacy => Pointer != 0 ? new(Pointer) : SoundHooks.Marker(this);

    internal SoundStyle(int type) : this()
    {
        lock (sounds)
        {
            if (!sounds.TryGetValue(type, out GameHandle? sound))
                sounds[type] = sound = new GameHandle(LegacySoundStyle.New(type, 1).Pointer);
            Pointer = sound.Target;
        }
    }

    public SoundStyle(string soundPath, SoundType type = SoundType.Sound) : this()
    {
        SoundPath = soundPath;
        Type = type;
        volume = 1f;
        MaxInstances = 1;
        SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest;
    }

    public SoundStyle(string soundPath, int numberOfVariants, SoundType type = SoundType.Sound) : this(soundPath, 1, numberOfVariants, type) { }

    public SoundStyle(string soundPath, int variantSuffixesStart, int numberOfVariants, SoundType type = SoundType.Sound) : this(soundPath, type)
    {
        if (numberOfVariants > 1)
        {
            variants = new int[numberOfVariants];
            for (int i = 0; i < numberOfVariants; i++) variants[i] = variantSuffixesStart + i;
        }
    }

    public SoundStyle(string soundPath, ReadOnlySpan<int> variants, SoundType type = SoundType.Sound) : this(soundPath, type)
    {
        this.variants = variants.IsEmpty ? null : variants.ToArray();
    }

    public SoundStyle WithVolumeScale(float scale) => this with { Volume = Volume * scale };

    public SoundStyle WithPitchOffset(float offset) => this with { Pitch = Pitch + offset };

    public static implicit operator SoundStyle(int type) => new(type);

    public static implicit operator SoundStyle(LegacySoundStyle style) => new(style.Pointer);

    public static implicit operator LegacySoundStyle(SoundStyle style) => style.Legacy;
}

public enum SoundLimitBehavior
{
    IgnoreNew,
    ReplaceOldest,
}
