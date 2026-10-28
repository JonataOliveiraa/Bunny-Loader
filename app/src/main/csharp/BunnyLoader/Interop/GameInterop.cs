using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace BunnyLoader.Interop;

public sealed unsafe class GameClass
{
    readonly string ns, name, nested;
    IntPtr handle;

    public GameClass(string ns, string name, string nested = "")
    {
        this.ns = ns;
        this.name = name;
        this.nested = nested;
    }

    public string FullName => nested.Length == 0 ? $"{ns}.{name}" : $"{ns}.{name}/{nested}";

    public IntPtr Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            IntPtr h = handle;
            return h != IntPtr.Zero ? h : Resolve();
        }
    }

    IntPtr Resolve()
    {
        IntPtr h = GameInterop.FindClass(ns, name);
        if (h != IntPtr.Zero && nested.Length > 0)
        {
            foreach (string part in nested.Split('/'))
            {
                byte* p = Host.Utf8(part);
                try { h = Host.Native()->FindNestedClass(h, p); }
                finally { Host.FreeUtf8(p); }
                if (h == IntPtr.Zero) break;
            }
        }
        if (h == IntPtr.Zero) throw new TypeLoadException($"game class {FullName} not found");
        Volatile.Write(ref handle, h);
        return h;
    }
}

public struct MethodRef
{
    public IntPtr Info;
    public IntPtr Fn;
}

public sealed class VirtualCache
{
    public readonly IntPtr Klass, Info, Fn;

    public VirtualCache(IntPtr klass, IntPtr info, IntPtr fn)
    {
        Klass = klass;
        Info = info;
        Fn = fn;
    }
}

public struct StaticRef
{
    public IntPtr Address;
    public IntPtr Field;
}

public static unsafe class GameInterop
{
    public static IntPtr FindClass(string ns, string name)
    {
        byte* n = Host.Utf8(ns), c = Host.Utf8(name);
        try { return Host.Native()->FindClass(n, c); }
        finally { Host.FreeUtf8(n); Host.FreeUtf8(c); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Offset(ref int slot, GameClass cls, string field)
    {
        int offset = slot;
        return offset >= 0 ? offset : ResolveOffset(ref slot, cls, field);
    }

    static int ResolveOffset(ref int slot, GameClass cls, string field)
    {
        byte* f = Host.Utf8(field);
        int offset;
        try { offset = Host.Native()->FieldOffset(cls.Handle, f); }
        finally { Host.FreeUtf8(f); }
        if (offset < 0) throw new MissingFieldException($"{cls.FullName}.{field} not found");
        slot = offset;
        return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly MethodRef Method(ref MethodRef slot, GameClass cls, string signature, int[] layout)
    {
        if (slot.Fn == IntPtr.Zero) ResolveMethod(ref slot, cls, signature, layout);
        return ref slot;
    }

    static void ResolveMethod(ref MethodRef slot, GameClass cls, string signature, int[] layout)
    {
        IntPtr info;
        byte* s = Host.Utf8(signature);
        try { info = Host.Native()->FindMethodBySignature(cls.Handle, s); }
        finally { Host.FreeUtf8(s); }
        if (info == IntPtr.Zero) throw new MissingMethodException($"{cls.FullName}: '{signature}' not found");
        fixed (int* sizes = layout)
        {
            int bad = Host.Native()->CheckLayout(info, sizes, layout.Length);
            if (bad >= 0)
                throw new InvalidOperationException(
                    $"{cls.FullName}: '{signature}': struct layout of {(bad == layout.Length - 1 ? "the return value" : $"parameter {bad + 1}")} differs from the game");
        }
        slot.Info = info;
        Volatile.Write(ref slot.Fn, Host.Native()->MethodPointer(info));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VirtualCache Virtual(ref VirtualCache? cache, in MethodRef method, IntPtr self)
    {
        if (self == IntPtr.Zero) throw new NullReferenceException("game object is null");
        IntPtr klass = *(IntPtr*)self;
        VirtualCache? c = cache;
        return c != null && c.Klass == klass ? c : ResolveVirtual(ref cache, method, self, klass);
    }

    static VirtualCache ResolveVirtual(ref VirtualCache? cache, in MethodRef method, IntPtr self, IntPtr klass)
    {
        IntPtr info = Host.Native()->VirtualMethod(self, method.Info);
        var c = new VirtualCache(klass, info, info == method.Info ? method.Fn : Host.Native()->MethodPointer(info));
        cache = c;
        return c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Call(IntPtr fn, CallFrame* frame)
    {
        if (Host.Api->CallGame(fn, frame) == 0) ThrowGame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CallFast(IntPtr fn, CallFrame* frame)
    {
        var call = (delegate* unmanaged[SuppressGCTransition]<IntPtr, CallFrame*, int>)Host.Api->CallGame;
        if (call(fn, frame) == 0) ThrowGame();
    }

    static void ThrowGame() => throw new GameException(Marshal.PtrToStringUTF8((IntPtr)Host.Native()->LastCallError()) ?? "game exception");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref StaticRef Static(ref StaticRef slot, GameClass cls, string field)
    {
        if (slot.Field == IntPtr.Zero) ResolveStatic(ref slot, cls, field);
        return ref slot;
    }

    static void ResolveStatic(ref StaticRef slot, GameClass cls, string field)
    {
        IntPtr info;
        byte* f = Host.Utf8(field);
        try { info = Host.Native()->FindField(cls.Handle, f); }
        finally { Host.FreeUtf8(f); }
        if (info == IntPtr.Zero) throw new MissingFieldException($"{cls.FullName}.{field} not found");
        slot.Address = Host.Native()->StaticFieldAddress(info);
        Volatile.Write(ref slot.Field, info);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr StaticAddress(ref StaticRef slot, GameClass cls, string field)
    {
        IntPtr address = slot.Address;
        if (address != IntPtr.Zero) return address;
        ref StaticRef s = ref Static(ref slot, cls, field);
        if (s.Address == IntPtr.Zero) throw new NotSupportedException($"{cls.FullName}.{field}: static field has no fixed address");
        return s.Address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadStatic<T>(ref StaticRef slot, GameClass cls, string field) where T : unmanaged
    {
        ref StaticRef s = ref Static(ref slot, cls, field);
        if (s.Address != IntPtr.Zero) return *(T*)s.Address;
        T value;
        Host.Native()->StaticFieldGet(s.Field, &value);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic<T>(ref StaticRef slot, GameClass cls, string field, T value, bool reference) where T : unmanaged
    {
        ref StaticRef s = ref Static(ref slot, cls, field);
        if (s.Address != IntPtr.Zero)
        {
            *(T*)s.Address = value;
            return;
        }
        Host.Native()->StaticFieldSet(s.Field, reference ? *(void**)&value : &value);
    }

    public static IntPtr NewObject(GameClass cls)
    {
        IntPtr obj = Host.Native()->NewObject(cls.Handle);
        if (obj == IntPtr.Zero) throw new OutOfMemoryException($"could not allocate {cls.FullName}");
        return obj;
    }

    public static int NativeNoOp() => Host.Native()->ClassValueSize(IntPtr.Zero);

    public static bool IsInstance(IntPtr obj, GameClass cls) =>
        obj != IntPtr.Zero && Host.Native()->IsInstance(obj, cls.Handle) != 0;

    public static string ClassNameOf(IntPtr obj) =>
        obj == IntPtr.Zero ? "null" : Marshal.PtrToStringUTF8((IntPtr)Host.Native()->ClassName(*(IntPtr*)obj)) ?? "?";
}

public sealed class GameException : Exception
{
    public GameException(string message) : base(message) { }
}
