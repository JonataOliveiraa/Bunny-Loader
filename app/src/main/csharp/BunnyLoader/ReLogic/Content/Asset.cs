using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;

namespace ReLogic.Content;

// Um pedido de ModContent.Request ou Mod.Assets.Request, como o Asset<T> do
// tModLoader. Por baixo e o Asset<Texture2D> do proprio jogo.
//
// A textura so nasce na thread do jogo (a Unity aborta o processo se ela
// nasce fora dela), e os mods carregam em outra: o pedido no Load fica
// NotLoaded, e carrega na primeira leitura do Value (ou no Wait) na thread do
// jogo. Por enquanto so Texture2D.
public sealed unsafe class Asset<T> where T : struct, IGameObject
{
    // O PNG do mod; null = uma imagem do jogo (Name).
    readonly string? file;

    // O Asset<Texture2D> do jogo, 0 ate carregar. O nativo o segura vivo.
    IntPtr asset;

    internal Asset(string name, string? file)
    {
        Name = name;
        this.file = file;
    }

    public string Name { get; }

    public AssetState State => Volatile.Read(ref asset) != IntPtr.Zero ? AssetState.Loaded : AssetState.NotLoaded;

    public bool IsLoaded => State == AssetState.Loaded;

    public bool IsDisposed => false;

    // Carrega se precisar (so na thread do jogo).
    public T Value
    {
        get
        {
            Wait();
            IntPtr value = Host.Native()->TextureAssetValue(asset);
            return Unsafe.As<IntPtr, T>(ref value);
        }
    }

    // Carrega agora. Fora da thread do jogo (no Load, por exemplo), erro.
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

    // O pedido de um tipo que ainda nao tem como carregar.
    internal static void CheckSupported()
    {
        if (typeof(T) != typeof(Texture2D))
            throw new NotSupportedException($"Asset<{typeof(T).Name}>: only Texture2D can be requested for now");
    }
}
