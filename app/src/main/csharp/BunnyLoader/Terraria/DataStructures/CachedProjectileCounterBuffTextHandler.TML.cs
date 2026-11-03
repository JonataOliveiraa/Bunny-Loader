using System;
using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.DataStructures;

public readonly partial struct CachedProjectileCounterBuffTextHandler
{
    static readonly List<GameHandle> Kept = new();

    public CachedProjectileCounterBuffTextHandler(params int[] projectileTypesToCount)
    {
        GameArray<int> types = GameArray<int>.New(new GameClass("System", "Int32"), projectileTypesToCount.Length);
        projectileTypesToCount.AsSpan().CopyTo(types.AsSpan());
        CachedProjectileCounterBuffTextHandler handler = New(types);
        lock (Kept) Kept.Add(new GameHandle(handler.Pointer));
        Pointer = handler.Pointer;
    }
}
