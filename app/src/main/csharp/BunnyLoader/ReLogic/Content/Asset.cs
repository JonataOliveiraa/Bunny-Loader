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
            IntPtr value = typeof(T) == typeof(Texture2D) ? Host.Native()->TextureAssetValue(asset) : *(IntPtr*)(asset + ValueOffset());
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
