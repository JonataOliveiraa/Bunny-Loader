using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria.ModLoader.Core;

namespace Terraria.Audio;

public static partial class SoundEngine
{
    public static bool TryGetActiveSound(SlotId slot, out ActiveSound sound)
    {
        sound = GetActiveSound(slot);
        return !sound.IsNull;
    }

    public static SlotId PlaySound(in SoundStyle style, Vector2? position, SoundUpdateCallback updateCallback) =>
        Terraria.ModLoader.Core.ActiveSoundHooks.Play(style, position, updateCallback);

    public static SlotId PlaySound(in SoundStyle style, Vector2? position = null)
    {
        if (style.Pointer != 0)
        {
            return Terraria.ModLoader.Core.ActiveSoundHooks.Play(style, position, null);
        }
        int handle = position is Vector2 p ? SoundHooks.Play(style, (int)p.X, (int)p.Y) : SoundHooks.Play(style, -1, -1);
        return new SlotId { Value = handle > 0 ? (uint)handle : 0 };
    }
}
