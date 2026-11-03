using System;
using BunnyLoader;
using BunnyLoader.Interop;

namespace ReLogic.Content;

public readonly unsafe struct AssetArray<T> : IGameObject where T : struct, IGameObject
{
    public readonly IntPtr Pointer;

    public AssetArray(IntPtr pointer) => Pointer = pointer;

    IntPtr IGameObject.Pointer => Pointer;

    public bool IsNull => Pointer == IntPtr.Zero;

    internal GameArray<GameObject> Raw => new(Pointer);

    public int Length => new GameArray<GameObject>(Pointer).Length;

    public Asset<T> this[int index]
    {
        get => Asset<T>.FromGame(new GameArray<GameObject>(Pointer)[index].Pointer);
        set => new GameArray<GameObject>(Pointer)[index] = new GameObject(Asset<T>.ToGame(value));
    }

    public AssetArray<T> Resized(int length) => new(Host.Native()->ResizeArray(Pointer, length));

    public static implicit operator GameArray<GameObject>(AssetArray<T> array) => new(array.Pointer);

    public static implicit operator AssetArray<T>(GameArray<GameObject> array) => new(array.Pointer);
}
