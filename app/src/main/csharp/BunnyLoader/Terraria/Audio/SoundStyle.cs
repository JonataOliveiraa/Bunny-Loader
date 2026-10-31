using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.Audio;

public readonly partial struct SoundStyle
{
    static readonly Dictionary<int, GameHandle> sounds = new();

    internal LegacySoundStyle Legacy => new(Pointer);

    internal SoundStyle(int type)
    {
        lock (sounds)
        {
            if (!sounds.TryGetValue(type, out GameHandle? sound))
                sounds[type] = sound = new GameHandle(LegacySoundStyle.New(type, 1).Pointer);
            Pointer = sound.Target;
        }
    }

    public static implicit operator SoundStyle(int type) => new(type);

    public static implicit operator SoundStyle(LegacySoundStyle style) => new(style.Pointer);
}
