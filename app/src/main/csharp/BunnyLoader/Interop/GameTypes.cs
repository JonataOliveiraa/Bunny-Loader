using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BunnyLoader.Interop;

// Toda proxy de objeto do jogo: so o ponteiro. Copiar uma proxy nao copia o
// objeto, e a proxy nao o segura vivo: guardar uma entre quadros pede um
// GameHandle (o coletor do jogo nao enxerga a memoria do C#).
public interface IGameObject
{
    IntPtr Pointer { get; }
}

// Um objeto do jogo sem tipo conhecido (object, interfaces, genericos).
public readonly struct GameObject : IGameObject, IEquatable<GameObject>
{
    public readonly IntPtr Pointer;

    public GameObject(IntPtr pointer) => Pointer = pointer;

    IntPtr IGameObject.Pointer => Pointer;

    public bool IsNull => Pointer == IntPtr.Zero;

    public string ClassName => GameInterop.ClassNameOf(Pointer);

    public bool Equals(GameObject other) => Pointer == other.Pointer;

    public override bool Equals(object? obj) => obj is GameObject o && Equals(o);

    public override int GetHashCode() => Pointer.GetHashCode();

    public static bool operator ==(GameObject a, GameObject b) => a.Pointer == b.Pointer;

    public static bool operator !=(GameObject a, GameObject b) => a.Pointer != b.Pointer;

    public override string ToString() => IsNull ? "null" : $"{ClassName}@{Pointer:x}";
}

// Uma string do jogo (System.String do IL2CPP: tamanho em +0x10, UTF-16 em
// +0x14). Nos parametros e retornos as proxies ja convertem para string; esta
// aparece onde a referencia importa: ref/out e elementos de array.
public readonly unsafe struct GameString : IGameObject
{
    public readonly IntPtr Pointer;

    public GameString(IntPtr pointer) => Pointer = pointer;

    IntPtr IGameObject.Pointer => Pointer;

    public bool IsNull => Pointer == IntPtr.Zero;

    public int Length => Pointer == IntPtr.Zero ? 0 : *(int*)(Pointer + 0x10);

    public override string? ToString() => Read(Pointer);

    public static string? Read(IntPtr str)
    {
        if (str == IntPtr.Zero) return null;
        return new string((char*)(str + 0x14), 0, *(int*)(str + 0x10));
    }

    public static IntPtr New(string? value)
    {
        if (value == null) return IntPtr.Zero;
        fixed (char* chars = value) return Host.Native()->NewString(chars, value.Length);
    }

    public static implicit operator string?(GameString s) => Read(s.Pointer);

    public static GameString From(string? value) => new GameString(New(value));
}

// Um array do jogo. Elemento de referencia e uma proxy (um ponteiro), elemento
// de valor e o proprio struct: os dois tem o tamanho certo para o indice.
// Layout do Il2CppArray: max_length em +0x18, dados em +0x20.
public readonly unsafe struct GameArray<T> : IGameObject where T : unmanaged
{
    public readonly IntPtr Pointer;

    public GameArray(IntPtr pointer) => Pointer = pointer;

    public static GameArray<T> New(GameClass element, int length) => new(Host.Native()->NewArray(element.Handle, length));

    IntPtr IGameObject.Pointer => Pointer;

    public bool IsNull => Pointer == IntPtr.Zero;

    public int Length => (int)*(long*)(Pointer + 0x18);

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)*(long*)(Pointer + 0x18)) ThrowIndex(index);
            return ref *(T*)(Pointer + 0x20 + (nint)index * sizeof(T));
        }
    }

    public Span<T> AsSpan() => new Span<T>((void*)(Pointer + 0x20), Length);

    static void ThrowIndex(int index) => throw new IndexOutOfRangeException($"index {index} out of the game array");
}

// Um array de duas dimensoes do jogo (T[,]): os limites ficam num bloco
// apontado por +0x10, 16 bytes por dimensao (tamanho, limite inferior).
public readonly unsafe struct GameArray2D<T> : IGameObject where T : unmanaged
{
    public readonly IntPtr Pointer;

    public GameArray2D(IntPtr pointer) => Pointer = pointer;

    IntPtr IGameObject.Pointer => Pointer;

    public bool IsNull => Pointer == IntPtr.Zero;

    public int GetLength(int dimension) => (int)*(long*)(*(IntPtr*)(Pointer + 0x10) + dimension * 16);

    public ref T this[int i, int j]
    {
        get
        {
            long* bounds = *(long**)(Pointer + 0x10);
            long rows = bounds[0], cols = bounds[2];
            if ((ulong)i >= (ulong)rows || (ulong)j >= (ulong)cols) throw new IndexOutOfRangeException($"index [{i}, {j}] out of the game array");
            return ref *(T*)(Pointer + 0x20 + (nint)(i * cols + j) * sizeof(T));
        }
    }
}

// O Nullable<T> do jogo: o mesmo layout do IL2CPP (hasValue no byte 0, o
// valor alinhado depois). O `T?` do C# tem o mesmo layout, mas nao conta como
// unmanaged e nao entra em ponteiro nem em GameArray; as conversoes deixam o
// mod escrever `5f`, `null` ou ler como `float?`.
[StructLayout(LayoutKind.Sequential)]
public struct GameNullable<T> where T : unmanaged
{
    bool hasValue;
    T value;

    public GameNullable(T value)
    {
        hasValue = true;
        this.value = value;
    }

    public readonly bool HasValue => hasValue;

    public readonly T Value => hasValue ? value : throw new InvalidOperationException("game nullable has no value");

    public readonly T GetValueOrDefault(T fallback = default) => hasValue ? value : fallback;

    public static implicit operator T?(GameNullable<T> v) => v.hasValue ? v.value : null;

    public static implicit operator GameNullable<T>(T? v) => v.HasValue ? new GameNullable<T>(v.Value) : default;

    public static implicit operator GameNullable<T>(T v) => new GameNullable<T>(v);

    public override readonly string ToString() => hasValue ? value.ToString() ?? "" : "null";
}

// Um objeto do jogo seguro vivo pelo coletor dele: para guardar entre quadros.
public sealed unsafe class GameHandle : IDisposable
{
    uint handle;

    public GameHandle(IntPtr obj) => handle = Host.Native()->GcHandleNew(obj, 0);

    public IntPtr Target => handle == 0 ? IntPtr.Zero : Host.Native()->GcHandleTarget(handle);

    public void Dispose()
    {
        if (handle != 0) Host.Native()->GcHandleFree(handle);
        handle = 0;
        GC.SuppressFinalize(this);
    }

    ~GameHandle()
    {
        if (handle != 0) Host.Native()->GcHandleFree(handle);
    }
}
