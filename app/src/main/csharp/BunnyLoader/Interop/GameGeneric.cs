using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BunnyLoader.Interop;

public static class GameGeneric
{
    static class Kind<T>
    {
        public static readonly bool Reference =
            typeof(IGameObject).IsAssignableFrom(typeof(T)) && Unsafe.SizeOf<T>() == IntPtr.Size;
    }

    static class ClassCache<T>
    {
        public static IntPtr Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReference<T>() => Kind<T>.Reference;

    public static IntPtr ClassOf<T>()
    {
        IntPtr handle = ClassCache<T>.Handle;
        if (handle != IntPtr.Zero) return handle;

        handle = FindClass(typeof(T)).Handle;
        ClassCache<T>.Handle = handle;
        return handle;
    }

    static GameClass FindClass(Type type)
    {
        if (type == typeof(GameObject)) return new GameClass("System", "Object");
        if (type == typeof(GameString)) return new GameClass("System", "String");

        FieldInfo? field = type.GetField("Class", BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is GameClass cls) return cls;
        throw new NotSupportedException($"{type.FullName} is not a game class, so it cannot be a type argument of a game method");
    }

    public static NotSupportedException NotCompiled(string method, string compiled, params Type[] args)
    {
        string[] names = Array.ConvertAll(args, a => a.Name);
        return new NotSupportedException(
            $"{method}<{string.Join(", ", names)}> is not compiled into the game; it exists for: {compiled}");
    }
}
