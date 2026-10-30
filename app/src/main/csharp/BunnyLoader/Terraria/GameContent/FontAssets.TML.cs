using System;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria.GameContent;

public static partial class FontAssets
{
    static Asset<SpriteFont>? mouseText, itemStack, deathText;

    public static Asset<SpriteFont> MouseText => Wrap(ref mouseText, MouseText_, "Fonts/Mouse_Text");

    public static Asset<SpriteFont> ItemStack => Wrap(ref itemStack, ItemStack_, "Fonts/Item_Stack");

    public static Asset<SpriteFont> DeathText => Wrap(ref deathText, DeathText_, "Fonts/Death_Text");

    static Asset<SpriteFont> Wrap(ref Asset<SpriteFont>? cache, GameObject asset, string name)
    {
        if (asset.IsNull) throw new InvalidOperationException($"FontAssets: {name} is not loaded yet");
        Asset<SpriteFont>? cached = cache;
        if (cached != null && cached.GameAsset == asset.Pointer) return cached;
        return cache = new Asset<SpriteFont>(name, asset.Pointer);
    }
}
