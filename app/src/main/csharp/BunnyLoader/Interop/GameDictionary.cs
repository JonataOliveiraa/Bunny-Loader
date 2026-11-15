using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace BunnyLoader.Interop;

public readonly unsafe struct GameDictionary
{
    const int CountMethod = 0, TryGetValueMethod = 1, ContainsKeyMethod = 2, SetItemMethod = 3, RemoveMethod = 4;

    sealed class Shape
    {
        internal readonly IntPtr Class;
        internal int EntriesOffset = -1, CountOffset = -1, FreeCountOffset = -1;
        internal readonly IntPtr[] Infos = new IntPtr[5], Pointers = new IntPtr[5];

        internal Shape(IntPtr cls) => Class = cls;
    }

    static readonly ConcurrentDictionary<IntPtr, Shape> Shapes = new();
    static Shape? last;

    readonly IntPtr dictionary;

    public GameDictionary(IntPtr dictionary) => this.dictionary = dictionary;

    public GameDictionary(GameObject dictionary) : this(dictionary.Pointer) { }

    public bool IsNull => dictionary == IntPtr.Zero;

    Shape Layout
    {
        get
        {
            IntPtr cls = *(IntPtr*)dictionary;
            Shape? shape = Volatile.Read(ref last);
            if (shape != null && shape.Class == cls) return shape;
            shape = Shapes.GetOrAdd(cls, static c => new Shape(c));
            Volatile.Write(ref last, shape);
            return shape;
        }
    }

    static IntPtr Method(Shape shape, int slot, string name, int argc, out IntPtr info)
    {
        IntPtr fn = Volatile.Read(ref shape.Pointers[slot]);
        if (fn != IntPtr.Zero)
        {
            info = shape.Infos[slot];
            return fn;
        }
        byte* n = Host.Utf8(name);
        try { info = Host.Native()->FindMethod(shape.Class, n, argc); }
        finally { Host.FreeUtf8(n); }
        if (info == IntPtr.Zero) throw new MissingMethodException($"game Dictionary: no {name} with {argc} argument(s)");
        fn = Host.Native()->MethodPointer(info);
        shape.Infos[slot] = info;
        Volatile.Write(ref shape.Pointers[slot], fn);
        return fn;
    }

    static void Value<T>(ref GameCall call, T* value) where T : unmanaged
    {
        if (typeof(T) == typeof(Microsoft.Xna.Framework.Vector2))
        {
            var vector = *(Microsoft.Xna.Framework.Vector2*)value;
            call.Arg(vector.X);
            call.Arg(vector.Y);
        }
        else if (sizeof(T) > 16) call.Arg((IntPtr)value);
        else call.Arg(*value);
    }

    public int Count
    {
        get
        {
            IntPtr fn = Method(Layout, CountMethod, "get_Count", 0, out IntPtr info);
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(dictionary);
            call.Arg(info);
            return (int)call.Invoke(fn);
        }
    }

    public bool TryGetValue<TKey, TValue>(TKey key, out TValue value) where TKey : unmanaged where TValue : unmanaged
    {
        IntPtr fn = Method(Layout, TryGetValueMethod, "TryGetValue", 2, out IntPtr info);
        TValue result = default;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        call.Arg((IntPtr)(&result));
        call.Arg(info);
        bool found = (call.Invoke(fn) & 0xff) != 0;
        value = result;
        return found;
    }

    public bool ContainsKey<TKey>(TKey key) where TKey : unmanaged
    {
        IntPtr fn = Method(Layout, ContainsKeyMethod, "ContainsKey", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        call.Arg(info);
        return (call.Invoke(fn) & 0xff) != 0;
    }

    public void Set<TKey, TValue>(TKey key, TValue value) where TKey : unmanaged where TValue : unmanaged
    {
        IntPtr fn = Method(Layout, SetItemMethod, "set_Item", 2, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        Value(ref call, &value);
        call.Arg(info);
        call.Invoke(fn);
    }

    public KeyValuePair<int, int>[] IntEntries()
    {
        Shape shape = Layout;
        IntPtr entries = *(IntPtr*)(dictionary + Offset(shape.Class, ref shape.EntriesOffset, "_entries"));
        int count = *(int*)(dictionary + Offset(shape.Class, ref shape.CountOffset, "_count"));
        int free = *(int*)(dictionary + Offset(shape.Class, ref shape.FreeCountOffset, "_freeCount"));
        var result = new List<KeyValuePair<int, int>>(count);
        if (entries == IntPtr.Zero) return result.ToArray();
        int* entry = (int*)(entries + 0x20);
        for (int i = 0; i < count; i++, entry += 4)
        {
            if (free > 0 && (entry[0] == -1 || entry[1] < -1)) continue;
            result.Add(new KeyValuePair<int, int>(entry[2], entry[3]));
        }
        return result.ToArray();
    }

    static int Offset(IntPtr cls, ref int slot, string field)
    {
        int offset = Volatile.Read(ref slot);
        if (offset >= 0) return offset;
        byte* name = Host.Utf8(field);
        try { offset = Host.Native()->FieldOffset(cls, name); }
        finally { Host.FreeUtf8(name); }
        if (offset < 0) throw new MissingFieldException($"game Dictionary: no field {field}");
        Volatile.Write(ref slot, offset);
        return offset;
    }

    public bool Remove<TKey>(TKey key) where TKey : unmanaged
    {
        IntPtr fn = Method(Layout, RemoveMethod, "Remove", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        call.Arg(info);
        return (call.Invoke(fn) & 0xff) != 0;
    }
}
