using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria;

public readonly unsafe partial struct Animation
{
    static readonly Dictionary<int, (int FrameRate, int[] Frames)> AnimationFrameDatas = new();

    static int nextRegisteredType = 100;

    public static int RegisterTemporaryAnimation(int frameRate, int[] frames)
    {
        lock (AnimationFrameDatas)
        {
            if (AnimationFrameDatas.Count == 0)
                On_Animation.SetDefaults += SetRegisteredDefaults;
            int type = nextRegisteredType++;
            AnimationFrameDatas[type] = (frameRate, frames);
            return type;
        }
    }

    static void SetRegisteredDefaults(On_Animation.orig_SetDefaults orig, Animation self, int type)
    {
        orig(self, type);
        (int FrameRate, int[] Frames) data;
        lock (AnimationFrameDatas)
            if (!AnimationFrameDatas.TryGetValue(type, out data))
                return;
        self._frameMax = data.Frames.Length;
        self._frameCounterMax = data.FrameRate;
        GameArray<int> frames = GameArray<int>.New(new GameClass("System", "Int32"), data.Frames.Length);
        for (int k = 0; k < data.Frames.Length; k++)
            frames[k] = data.Frames[k];
        self._frameData = frames;
    }
}
