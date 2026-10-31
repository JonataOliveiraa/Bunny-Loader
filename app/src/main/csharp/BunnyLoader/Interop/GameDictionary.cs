using System;
using System.Collections.Concurrent;

namespace BunnyLoader.Interop;

public readonly unsafe struct GameDictionary
{
    static readonly ConcurrentDictionary<(IntPtr, string, int), IntPtr> Methods = new();

    readonly IntPtr dictionary;

    public GameDictionary(IntPtr dictionary) => this.dictionary = dictionary;

    public GameDictionary(GameObject dictionary) : this(dictionary.Pointer) { }

    public bool IsNull => dictionary == IntPtr.Zero;

    IntPtr Class => *(IntPtr*)dictionary;

    IntPtr Method(string name, int argc, out IntPtr info)
    {
        info = Methods.GetOrAdd((Class, name, argc), key =>
        {
            byte* n = Host.Utf8(key.Item2);
            try
            {
                IntPtr method = Host.Native()->FindMethod(key.Item1, n, key.Item3);
                if (method == IntPtr.Zero) throw new MissingMethodException($"game Dictionary: no {key.Item2} with {key.Item3} argument(s)");
                return method;
            }
            finally { Host.FreeUtf8(n); }
        });
        return Host.Native()->MethodPointer(info);
    }

    static void Value<T>(ref GameCall call, T* value) where T : unmanaged
    {
        if (sizeof(T) > 16) call.Arg((IntPtr)value);
        else call.Arg(*value);
    }

    public bool TryGetValue<TKey, TValue>(TKey key, out TValue value) where TKey : unmanaged where TValue : unmanaged
    {
        IntPtr fn = Method("TryGetValue", 2, out IntPtr info);
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
        IntPtr fn = Method("ContainsKey", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        call.Arg(info);
        return (call.Invoke(fn) & 0xff) != 0;
    }

    public void Set<TKey, TValue>(TKey key, TValue value) where TKey : unmanaged where TValue : unmanaged
    {
        IntPtr fn = Method("set_Item", 2, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        Value(ref call, &value);
        call.Arg(info);
        call.Invoke(fn);
    }

    public bool Remove<TKey>(TKey key) where TKey : unmanaged
    {
        IntPtr fn = Method("Remove", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(dictionary);
        call.Arg(key);
        call.Arg(info);
        return (call.Invoke(fn) & 0xff) != 0;
    }
}
