using System;

namespace BunnyLoader.Interop;

public static class GameSlot<T>
{
    const int Size = 32;

    [ThreadStatic]
    static T[]? ring;

    [ThreadStatic]
    static int next;

    public static ref readonly T Hold(T value)
    {
        T[] slots = ring ??= new T[Size];
        ref T slot = ref slots[next++ & (Size - 1)];
        slot = value;
        return ref slot;
    }
}
