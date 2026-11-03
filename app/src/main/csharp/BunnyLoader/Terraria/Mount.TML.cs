using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria;

public readonly partial struct Mount
{
    static readonly Dictionary<IntPtr, object?> SpecificData = new();

    public object? _mountSpecificData
    {
        get
        {
            lock (SpecificData) return SpecificData.TryGetValue(Pointer, out object? data) ? data : null;
        }
        set
        {
            lock (SpecificData)
            {
                if (value == null) SpecificData.Remove(Pointer);
                else SpecificData[Pointer] = value;
            }
        }
    }

    public readonly partial struct MountData
    {
        public int[] playerYOffsets
        {
            get
            {
                GameArray<int> offsets = playerYOffsets_;
                return offsets.IsNull ? null! : offsets.AsSpan().ToArray();
            }
            set
            {
                if (value == null)
                {
                    playerYOffsets_ = default;
                    return;
                }
                GameArray<int> offsets = GameArray<int>.New(new GameClass("System", "Int32", ""), value.Length);
                value.AsSpan().CopyTo(offsets.AsSpan());
                playerYOffsets_ = offsets;
            }
        }
    }
}
