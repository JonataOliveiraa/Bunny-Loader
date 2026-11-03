using System;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Terraria;

public readonly partial struct Mount
{
    public readonly partial struct MountDelegatesData
    {
        public Action<Vector2>? MinecartDust
        {
            get => MountDelegates.Dust(MinecartDust_);
            set => MinecartDust_ = MountDelegates.Dust(value);
        }

        public Action<Player, Vector2, int, int>? MinecartJumpingSound
        {
            get => MountDelegates.Sound(MinecartJumpingSound_);
            set => MinecartJumpingSound_ = MountDelegates.Sound(value);
        }

        public Action<Player, Vector2, int, int>? MinecartLandingSound
        {
            get => MountDelegates.Sound(MinecartLandingSound_);
            set => MinecartLandingSound_ = MountDelegates.Sound(value);
        }

        public Action<Player, Vector2, int, int>? MinecartBumperSound
        {
            get => MountDelegates.Sound(MinecartBumperSound_);
            set => MinecartBumperSound_ = MountDelegates.Sound(value);
        }
    }
}

internal static unsafe class MountDelegates
{
    static IntPtr dustClass, soundClass;

    static IntPtr ClassOf(GameObject template, ref IntPtr cached, string what)
    {
        if (cached != IntPtr.Zero) return cached;
        if (template.Pointer == IntPtr.Zero) throw new InvalidOperationException($"no game {what} delegate to copy the class from");
        return cached = *(IntPtr*)template.Pointer;
    }

    static Mount.MountDelegatesData Minecart => Mount.mounts[MountID.Minecart].delegations;

    internal static GameObject Dust(Action<Vector2>? handler)
    {
        if (handler == null) return default;
        IntPtr klass = ClassOf(Minecart.MinecartDust_, ref dustClass, "minecart dust");
        return new GameObject(GameDelegates.Create(handler, klass, (IntPtr)(delegate* unmanaged<long, float, float, long, void>)&DustInvoke));
    }

    internal static GameObject Sound(Action<Player, Vector2, int, int>? handler)
    {
        if (handler == null) return default;
        IntPtr klass = ClassOf(Minecart.MinecartLandingSound_, ref soundClass, "minecart sound");
        return new GameObject(GameDelegates.Create(handler, klass, (IntPtr)(delegate* unmanaged<long, long, float, float, long, long, long, void>)&SoundInvoke));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    static void DustInvoke(long target, float x, float y, long method)
    {
        try { GameDelegates.Get<Action<Vector2>>((IntPtr)target)?.Invoke(new Vector2(x, y)); }
        catch (Exception e) { GameDelegates.Report("delegate Action<Vector2>", e); }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    static void SoundInvoke(long target, long player, float x, float y, long width, long height, long method)
    {
        try { GameDelegates.Get<Action<Player, Vector2, int, int>>((IntPtr)target)?.Invoke(new Player((IntPtr)player), new Vector2(x, y), (int)width, (int)height); }
        catch (Exception e) { GameDelegates.Report("delegate Action<Player, Vector2, int, int>", e); }
    }

    internal static Action<Vector2>? Dust(GameObject game)
    {
        if (game.Pointer == IntPtr.Zero) return null;
        IntPtr fn = Invoke(game.Pointer, 1, out IntPtr info);
        return position =>
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(game.Pointer);
            call.Arg(position.X);
            call.Arg(position.Y);
            call.Arg(info);
            call.Invoke(fn);
        };
    }

    internal static Action<Player, Vector2, int, int>? Sound(GameObject game)
    {
        if (game.Pointer == IntPtr.Zero) return null;
        IntPtr fn = Invoke(game.Pointer, 4, out IntPtr info);
        return (player, position, width, height) =>
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(game.Pointer);
            call.Arg(player.Pointer);
            call.Arg(position.X);
            call.Arg(position.Y);
            call.Arg((long)width);
            call.Arg((long)height);
            call.Arg(info);
            call.Invoke(fn);
        };
    }

    static IntPtr Invoke(IntPtr game, int argc, out IntPtr info)
    {
        NativeApi* api = Host.Native();
        byte* name = Host.Utf8("Invoke");
        try { info = api->FindMethod(*(IntPtr*)game, name, argc); }
        finally { Host.FreeUtf8(name); }
        if (info == IntPtr.Zero) throw new MissingMethodException("game delegate without Invoke");
        return api->MethodPointer(info);
    }
}
