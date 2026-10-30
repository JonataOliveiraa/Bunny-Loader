using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;

namespace ReLogic.Content;

public sealed unsafe class Asset<T> where T : struct, IGameObject
{
    readonly string? file;

    IntPtr asset;

    internal Asset(string name, string? file)
    {
        Name = name;
        this.file = file;
    }

    internal Asset(string name, IntPtr gameAsset)
    {
        Name = name;
        asset = gameAsset;
    }

    internal IntPtr GameAsset => asset;

    IntPtr directTexture;

    internal bool IsDirect => directTexture != IntPtr.Zero;

    IntPtr Resolved()
    {
        if (directTexture != IntPtr.Zero) return directTexture;
        if (Volatile.Read(ref asset) == IntPtr.Zero && Host.Native()->IsGameThread() != 0) Wait();
        return asset;
    }

    public static bool operator ==(Asset<T>? left, Asset<T>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        IntPtr a = left.Resolved(), b = right.Resolved();
        if (a == IntPtr.Zero || b == IntPtr.Zero) return false;
        if (left.IsDirect == right.IsDirect) return a == b;
        return Unsafe.As<T, IntPtr>(ref Unsafe.AsRef(left.Value)) == Unsafe.As<T, IntPtr>(ref Unsafe.AsRef(right.Value));
    }

    public static bool operator !=(Asset<T>? left, Asset<T>? right) => !(left == right);

    public override bool Equals(object? obj) => obj is Asset<T> other && this == other;

    public override int GetHashCode() => Name.GetHashCode();

    static readonly System.Collections.Generic.Dictionary<IntPtr, Asset<T>> ByGameAsset = new();

    internal static Asset<T> ForGameAsset(IntPtr gameAsset, string name)
    {
        lock (ByGameAsset)
        {
            if (ByGameAsset.TryGetValue(gameAsset, out Asset<T>? known)) return known;
            var wrapped = new Asset<T>(name, gameAsset);
            ByGameAsset[gameAsset] = wrapped;
            return wrapped;
        }
    }

    internal static Asset<T> ForTexture(IntPtr texture)
    {
        IntPtr gameAsset = Host.Native()->AssetOfTexture(texture);
        if (gameAsset != IntPtr.Zero) return ForGameAsset(gameAsset, NameOf(gameAsset));
        return new Asset<T>("?", IntPtr.Zero) { directTexture = texture, asset = (IntPtr)1 };
    }

    static string NameOf(IntPtr gameAsset)
    {
        byte* field = Host.Utf8("<Name>k__BackingField");
        int offset;
        try { offset = Host.Native()->FieldOffset(*(IntPtr*)gameAsset, field); }
        finally { Host.FreeUtf8(field); }
        return offset >= 0 ? GameString.Read(*(IntPtr*)(gameAsset + offset)) ?? "?" : "?";
    }

    public string Name { get; }

    public AssetState State => Volatile.Read(ref asset) != IntPtr.Zero ? AssetState.Loaded : AssetState.NotLoaded;

    public bool IsLoaded => State == AssetState.Loaded;

    public bool IsDisposed => false;

    static int valueOffset = -1;

    public T Value
    {
        get
        {
            Wait();
            IntPtr value = directTexture != IntPtr.Zero ? directTexture
                : typeof(T) == typeof(Texture2D) ? Host.Native()->TextureAssetValue(asset) : *(IntPtr*)(asset + ValueOffset());
            return Unsafe.As<IntPtr, T>(ref value);
        }
    }

    int ValueOffset()
    {
        if (valueOffset >= 0) return valueOffset;
        byte* name = Host.Utf8("Value");
        try { valueOffset = Host.Native()->FieldOffset(*(IntPtr*)asset, name); }
        finally { Host.FreeUtf8(name); }
        if (valueOffset < 0) throw new MissingFieldException($"asset {Name}: the game's Asset has no Value field");
        return valueOffset;
    }

    public void Wait()
    {
        if (Volatile.Read(ref asset) != IntPtr.Zero) return;
        NativeApi* api = Host.Native();
        if (api->IsGameThread() == 0)
            throw new InvalidOperationException($"asset {Name}: textures only load on the game thread (SetStaticDefaults, hooks, drawing), not while the mod loads");
        IntPtr loaded;
        if (file != null)
        {
            byte* f = Host.Utf8(file), n = Host.Utf8(Name);
            try { loaded = api->RequestModTexture(f, n); }
            finally
            {
                Host.FreeUtf8(f);
                Host.FreeUtf8(n);
            }
            if (loaded == IntPtr.Zero) throw new InvalidOperationException($"asset {Name}: {file} is not an image the game can load");
        }
        else
        {
            byte* n = Host.Utf8(Name);
            int missing = 0;
            try { loaded = api->RequestVanillaTexture(n, &missing); }
            finally { Host.FreeUtf8(n); }
            if (missing != 0) throw new InvalidOperationException($"asset {Name}: the game image does not exist on mobile");
            if (loaded == IntPtr.Zero) throw new InvalidOperationException($"asset {Name}: no game texture with this name");
        }
        Volatile.Write(ref asset, loaded);
    }

    internal static void CheckSupported()
    {
        if (typeof(T) != typeof(Texture2D))
            throw new NotSupportedException($"Asset<{typeof(T).Name}>: only Texture2D can be requested for now");
    }
}
