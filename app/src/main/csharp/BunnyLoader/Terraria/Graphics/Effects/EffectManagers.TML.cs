using System;
using System.Collections.Concurrent;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;

namespace Terraria.Graphics.Effects;

internal static unsafe class EffectManagerCalls
{
    static readonly ConcurrentDictionary<(IntPtr, string, int), IntPtr> Methods = new();
    static GameArray<GameObject> noArgs;

    static IntPtr Method(IntPtr self, string name, int argc, out IntPtr info)
    {
        if (self == IntPtr.Zero) throw new NullReferenceException($"EffectManager.{name}: game object is null");
        IntPtr cls = *(IntPtr*)self;
        info = Methods.GetOrAdd((cls, name, argc), key =>
        {
            byte* n = Host.Utf8(key.Item2);
            try
            {
                IntPtr method = Host.Native()->FindMethod(key.Item1, n, key.Item3);
                if (method == IntPtr.Zero) throw new MissingMethodException($"EffectManager: no {key.Item2} with {key.Item3} argument(s)");
                return method;
            }
            finally { Host.FreeUtf8(n); }
        });
        return Host.Native()->MethodPointer(info);
    }

    static IntPtr NoArgs(object[] args)
    {
        if (args.Length > 0) throw new NotSupportedException("EffectManager: extra activation arguments are not supported on mobile");
        if (noArgs.IsNull) noArgs = GameArray<GameObject>.New(new GameClass("System", "Object"), 0);
        return noArgs.Pointer;
    }

    internal static IntPtr Get(IntPtr self, string key)
    {
        IntPtr fn = Method(self, "get_Item", 1, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(((GameString)key).Pointer);
        call.Arg(info);
        return (IntPtr)call.Invoke(fn);
    }

    internal static void Set(IntPtr self, string key, IntPtr value)
    {
        IntPtr fn = Method(self, "set_Item", 2, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(((GameString)key).Pointer);
        call.Arg(value);
        call.Arg(info);
        call.Invoke(fn);
    }

    internal static IntPtr Activate(IntPtr self, string name, Vector2 position, object[] args)
    {
        IntPtr fn = Method(self, "Activate", 3, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(((GameString)name).Pointer);
        call.Arg(position.X);
        call.Arg(position.Y);
        call.Arg(NoArgs(args));
        call.Arg(info);
        return (IntPtr)call.Invoke(fn);
    }

    internal static void Deactivate(IntPtr self, string name, object[] args)
    {
        IntPtr fn = Method(self, "Deactivate", 2, out IntPtr info);
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(((GameString)name).Pointer);
        call.Arg(NoArgs(args));
        call.Arg(info);
        call.Invoke(fn);
    }
}

public readonly partial struct FilterManager
{
    public Filter this[string key]
    {
        get => new(EffectManagerCalls.Get(Pointer, key));
        set => EffectManagerCalls.Set(Pointer, key, value.Pointer);
    }

    public Filter Activate(string name, Vector2 position = default, params object[] args) => new(EffectManagerCalls.Activate(Pointer, name, position, args));

    public void Deactivate(string name, params object[] args) => EffectManagerCalls.Deactivate(Pointer, name, args);
}

public readonly partial struct SkyManager
{
    public CustomSky this[string key]
    {
        get => new(EffectManagerCalls.Get(Pointer, key));
        set => EffectManagerCalls.Set(Pointer, key, value.Pointer);
    }

    public CustomSky Activate(string name, Vector2 position = default, params object[] args) => new(EffectManagerCalls.Activate(Pointer, name, position, args));

    public void Deactivate(string name, params object[] args) => EffectManagerCalls.Deactivate(Pointer, name, args);
}

public readonly partial struct OverlayManager
{
    public Overlay this[string key]
    {
        get => new(EffectManagerCalls.Get(Pointer, key));
        set => EffectManagerCalls.Set(Pointer, key, value.Pointer);
    }

    public Overlay Activate(string name, Vector2 position = default, params object[] args) => new(EffectManagerCalls.Activate(Pointer, name, position, args));

    public void Deactivate(string name, params object[] args) => EffectManagerCalls.Deactivate(Pointer, name, args);
}
