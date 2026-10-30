using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria;

public readonly partial struct Mount
{
    static readonly Dictionary<IntPtr, object?> SpecificData = new();

    public object? _mountSpecificData
    {
        get
        {
            lock (SpecificData) return SpecificData.TryGetValue(Pointer, out object? data) ? data : null;
        }
        set
        {
            lock (SpecificData)
            {
                if (value == null) SpecificData.Remove(Pointer);
                else SpecificData[Pointer] = value;
            }
        }
    }

    public readonly partial struct MountData
    {
        static Asset<Texture2D>? Get(GameObject asset) => asset.IsNull ? null : Asset<Texture2D>.ForGameAsset(asset.Pointer, "?");

        static GameObject Set(Asset<Texture2D>? value)
        {
            if (value == null) return default;
            if (value.IsDirect) throw new InvalidOperationException("MountData: this asset is a bare texture, not a game asset");
            value.Wait();
            return new GameObject(value.GameAsset);
        }

        public Asset<Texture2D>? backTexture { get => Get(backTexture_); set => backTexture_ = Set(value); }

        public Asset<Texture2D>? backTextureGlow { get => Get(backTextureGlow_); set => backTextureGlow_ = Set(value); }

        public Asset<Texture2D>? backTextureExtra { get => Get(backTextureExtra_); set => backTextureExtra_ = Set(value); }

        public Asset<Texture2D>? backTextureExtraGlow { get => Get(backTextureExtraGlow_); set => backTextureExtraGlow_ = Set(value); }

        public Asset<Texture2D>? frontTexture { get => Get(frontTexture_); set => frontTexture_ = Set(value); }

        public Asset<Texture2D>? frontTextureGlow { get => Get(frontTextureGlow_); set => frontTextureGlow_ = Set(value); }

        public Asset<Texture2D>? frontTextureExtra { get => Get(frontTextureExtra_); set => frontTextureExtra_ = Set(value); }

        public Asset<Texture2D>? frontTextureExtraGlow { get => Get(frontTextureExtraGlow_); set => frontTextureExtraGlow_ = Set(value); }

        public int[] playerYOffsets
        {
            get
            {
                GameArray<int> offsets = playerYOffsets_;
                return offsets.IsNull ? null! : offsets.AsSpan().ToArray();
            }
            set
            {
                if (value == null)
                {
                    playerYOffsets_ = default;
                    return;
                }
                GameArray<int> offsets = GameArray<int>.New(new GameClass("System", "Int32", ""), value.Length);
                value.AsSpan().CopyTo(offsets.AsSpan());
                playerYOffsets_ = offsets;
            }
        }
    }
}
