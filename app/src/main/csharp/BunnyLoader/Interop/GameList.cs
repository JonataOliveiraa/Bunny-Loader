using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BunnyLoader.Interop;

public readonly unsafe struct GameList
{
    const int SetItemMethod = 0, AddMethod = 1, InsertMethod = 2, RemoveAtMethod = 3;

    sealed class Shape
    {
        internal readonly IntPtr Class;
        internal int SizeOffset = -1, ItemsOffset = -1;
        internal readonly IntPtr[] Infos = new IntPtr[4], Pointers = new IntPtr[4];

        internal Shape(IntPtr cls) => Class = cls;
    }

    static readonly ConcurrentDictionary<IntPtr, Shape> Shapes = new();
    static Shape? last;

    readonly IntPtr list;

    public GameList(IntPtr list) => this.list = list;

    public GameList(GameObject list) : this(list.Pointer) { }

    public bool IsNull => list == IntPtr.Zero;

    Shape Layout
    {
        get
        {
            IntPtr cls = *(IntPtr*)list;
            Shape? shape = Volatile.Read(ref last);
            if (shape != null && shape.Class == cls) return shape;
            shape = Shapes.GetOrAdd(cls, static c => new Shape(c));
            Volatile.Write(ref last, shape);
            return shape;
        }
    }

    static int Offset(IntPtr cls, ref int slot, string field)
    {
        int offset = Volatile.Read(ref slot);
        if (offset >= 0) return offset;
        byte* name = Host.Utf8(field);
        try { offset = Host.Native()->FieldOffset(cls, name); }
        finally { Host.FreeUtf8(name); }
        if (offset < 0) throw new MissingFieldException($"game List: no field {field}");
        Volatile.Write(ref slot, offset);
        return offset;
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
        if (info == IntPtr.Zero) throw new MissingMethodException($"game List: no {name} with {argc} argument(s)");
        fn = Host.Native()->MethodPointer(info);
        shape.Infos[slot] = info;
        Volatile.Write(ref shape.Pointers[slot], fn);
        return fn;
    }

    IntPtr Items
    {
        get
        {
            Shape shape = Layout;
            return *(IntPtr*)(list + Offset(shape.Class, ref shape.ItemsOffset, "_items"));
        }
    }

    public int Count
    {
        get
        {
            Shape shape = Layout;
            return *(int*)(list + Offset(shape.Class, ref shape.SizeOffset, "_size"));
        }
    }

    public IntPtr this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new IndexOutOfRangeException($"index {index} out of the game List");
            return ((IntPtr*)(Items + 0x20))[index];
        }
        set
        {
            IntPtr fn = Method(Layout, SetItemMethod, "set_Item", 2, out IntPtr info);
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
        return ((T*)(Items + 0x20))[index];
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
        IntPtr fn = Method(Layout, AddMethod, "Add", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        if (sizeof(T) > 16) call.Arg((IntPtr)(&item));
        else call.Arg(item);
        call.Arg(info);
        call.Invoke(fn);
    }

    public void Insert(int index, IntPtr item)
    {
        if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        IntPtr fn = Method(Layout, InsertMethod, "Insert", 2, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        call.Arg(index);
        call.Arg(item);
        call.Arg(info);
        call.Invoke(fn);
    }

    public int IndexOf(IntPtr item)
    {
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            if (this[i] == item) return i;
        }
        return -1;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        IntPtr fn = Method(Layout, RemoveAtMethod, "RemoveAt", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(list);
        call.Arg(index);
        call.Arg(info);
        call.Invoke(fn);
    }
}
