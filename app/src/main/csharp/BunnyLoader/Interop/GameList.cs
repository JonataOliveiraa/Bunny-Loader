using System;
using System.Collections.Concurrent;

namespace BunnyLoader.Interop;

public readonly unsafe struct GameList
{
    static readonly ConcurrentDictionary<(IntPtr, string), int> Offsets = new();
    static readonly ConcurrentDictionary<(IntPtr, string, int), IntPtr> Methods = new();

    readonly IntPtr list;

    public GameList(IntPtr list) => this.list = list;

    public GameList(GameObject list) : this(list.Pointer) { }

    public bool IsNull => list == IntPtr.Zero;

    IntPtr Class => *(IntPtr*)list;

    int Offset(string field) =>
        Offsets.GetOrAdd((Class, field), key =>
        {
            byte* name = Host.Utf8(key.Item2);
            try
            {
                int offset = Host.Native()->FieldOffset(key.Item1, name);
                if (offset < 0) throw new MissingFieldException($"game List: no field {key.Item2}");
                return offset;
            }
            finally { Host.FreeUtf8(name); }
        });

    IntPtr Method(string name, int argc, out IntPtr info)
    {
        info = Methods.GetOrAdd((Class, name, argc), key =>
        {
            byte* n = Host.Utf8(key.Item2);
            try
            {
                IntPtr method = Host.Native()->FindMethod(key.Item1, n, key.Item3);
                if (method == IntPtr.Zero) throw new MissingMethodException($"game List: no {key.Item2} with {key.Item3} argument(s)");
                return method;
            }
            finally { Host.FreeUtf8(n); }
        });
        return Host.Native()->MethodPointer(info);
    }

    public int Count => *(int*)(list + Offset("_size"));

    public IntPtr this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new IndexOutOfRangeException($"index {index} out of the game List");
            IntPtr items = *(IntPtr*)(list + Offset("_items"));
            return ((IntPtr*)(items + 0x20))[index];
        }
        set
        {
            IntPtr fn = Method("set_Item", 2, out IntPtr info);
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(list);
            call.Arg(index);
            call.Arg(value);
            call.Arg(info);
            call.Invoke(fn);
        }
    }

    public T Get<T>(int index) where T : unmanaged
    {
        if ((uint)index >= (uint)Count) throw new IndexOutOfRangeException($"index {index} out of the game List");
        IntPtr items = *(IntPtr*)(list + Offset("_items"));
        return ((T*)(items + 0x20))[index];
    }

    public bool Contains<T>(T value) where T : unmanaged, IEquatable<T>
    {
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            if (Get<T>(i).Equals(value)) return true;
        }
        return false;
    }

    public void Add(IntPtr item) => Add<IntPtr>(item);

    public void Add<T>(T item) where T : unmanaged
    {
        IntPtr fn = Method("Add", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        if (sizeof(T) > 16) call.Arg((IntPtr)(&item));
        else call.Arg(item);
        call.Arg(info);
        call.Invoke(fn);
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        IntPtr fn = Method("RemoveAt", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        call.Arg(index);
        call.Arg(info);
        call.Invoke(fn);
    }
}
