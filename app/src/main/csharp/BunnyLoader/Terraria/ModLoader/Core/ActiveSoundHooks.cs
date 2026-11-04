using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria.Audio;

namespace Terraria.ModLoader.Core;

internal static unsafe class ActiveSoundHooks
{
    static readonly Dictionary<SlotId, SoundUpdateCallback> Callbacks = new();
    static readonly Func<bool> KeepLooping = static () => true;
    static MethodRef loopMethod;
    static readonly int[] LoopLayout = { -1, 8, -1, 8, 4 };

    internal static SlotId Play(in SoundStyle style, Vector2? position, SoundUpdateCallback? callback)
    {
        if (style.Pointer == 0) throw new NotSupportedException("Tracked sounds require a native SoundStyle; Android sound-pool playback does not expose active sound instances.");
        SlotId slot;
        if (style.IsLooped)
        {
            ref readonly MethodRef method = ref GameInterop.Method(ref loopMethod, SoundEngine.Class,
                "SlotId PlayTrackedLoopedSound(SoundStyle style, Vector2 position, ActiveSound.LoopedPlayCondition loopingCondition, SoundPlayOverrides overrides)", LoopLayout);
            IntPtr klass = GameDelegates.ParamClass(method.Info, 2);
            var condition = new ActiveSound.LoopedPlayCondition(GameDelegates.Create(KeepLooping, klass, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&Loop));
            using var pin = new GameHandle(condition.Pointer);
            slot = SoundEngine.PlayTrackedLoopedSound(style, position ?? Main.LocalPlayer.Center, condition, default);
        }
        else slot = position is Vector2 at ? SoundEngine.PlayTrackedSound(style, at, default) : SoundEngine.PlayTrackedSound(style);
        if (callback != null && SoundEngine.TryGetActiveSound(slot, out ActiveSound sound))
        {
            if (!callback(sound)) sound.Stop();
            else
            {
                Callbacks[slot] = callback;
                ItemHooks.Once("ActiveSoundCallbacks", () => On_SoundEngine.Update += Update);
            }
        }
        return slot;
    }

    [UnmanagedCallersOnly]
    static byte Loop(IntPtr target, IntPtr method) => 1;

    static void Update(On_SoundEngine.orig_Update orig)
    {
        foreach (var entry in new List<KeyValuePair<SlotId, SoundUpdateCallback>>(Callbacks))
        {
            if (!SoundEngine.TryGetActiveSound(entry.Key, out ActiveSound sound)) { Callbacks.Remove(entry.Key); continue; }
            try
            {
                if (entry.Value(sound)) continue;
            }
            catch (Exception e) { ItemHooks.Report("SoundUpdateCallback", e); }
            sound.Stop();
            Callbacks.Remove(entry.Key);
        }
        orig();
    }
}
