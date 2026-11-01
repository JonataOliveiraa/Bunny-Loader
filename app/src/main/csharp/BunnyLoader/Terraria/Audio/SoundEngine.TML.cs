using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria.ModLoader.Core;

namespace Terraria.Audio;

public static partial class SoundEngine
{
    public static SlotId PlaySound(in SoundStyle style, Vector2? position = null)
    {
        if (style.Pointer != 0)
        {
            if (position is Vector2 at) PlaySound(new LegacySoundStyle(style.Pointer), at);
            else PlaySound(new LegacySoundStyle(style.Pointer));
            return default;
        }
        int handle = position is Vector2 p ? SoundHooks.Play(style, (int)p.X, (int)p.Y) : SoundHooks.Play(style, -1, -1);
        return new SlotId { Value = handle > 0 ? (uint)handle : 0 };
    }
}
