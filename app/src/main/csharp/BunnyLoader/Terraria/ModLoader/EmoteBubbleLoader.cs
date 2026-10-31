using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class EmoteBubbleLoader
{
    static readonly object Gate = new();
    static readonly List<ModEmoteBubble> emoteBubbles = new();
    static readonly List<GlobalEmoteBubble> globalEmoteBubbles = new();
    static readonly Dictionary<int, List<ModEmoteBubble>> categories = new();
    static readonly Dictionary<int, Asset<Texture2D>> textures = new();
    static readonly Dictionary<IntPtr, ModEmoteBubble> instances = new();

    public static int EmoteBubbleCount => EmoteID.Count + emoteBubbles.Count;

    internal static IReadOnlyList<GlobalEmoteBubble> Globals => globalEmoteBubbles;

    internal static void Add(ModEmoteBubble emoteBubble)
    {
        lock (Gate)
        {
            emoteBubble.Type = EmoteID.Count + emoteBubbles.Count;
            emoteBubbles.Add(emoteBubble);
            textures[emoteBubble.Type] = new Asset<Texture2D>(emoteBubble.Texture, ItemLoader.ResolveTexture(emoteBubble, emoteBubble.Texture));
        }
        EmoteBubbleHooks.Install();
    }

    internal static void AddGlobal(GlobalEmoteBubble global)
    {
        lock (Gate) globalEmoteBubbles.Add(global);
        EmoteBubbleHooks.Install();
    }

    internal static void AddToCategory(ModEmoteBubble emoteBubble, int categoryId)
    {
        lock (Gate)
        {
            if (!categories.TryGetValue(categoryId, out List<ModEmoteBubble>? list)) categories[categoryId] = list = new List<ModEmoteBubble>();
            if (!list.Contains(emoteBubble)) list.Add(emoteBubble);
        }
    }

    internal static IReadOnlyList<ModEmoteBubble> InCategory(int categoryId)
    {
        lock (Gate) return categories.TryGetValue(categoryId, out List<ModEmoteBubble>? list) ? list.ToArray() : Array.Empty<ModEmoteBubble>();
    }

    public static ModEmoteBubble? GetEmoteBubble(int type) =>
        type >= EmoteID.Count && type < EmoteBubbleCount ? emoteBubbles[type - EmoteID.Count] : null;

    internal static Texture2D TextureOf(ModEmoteBubble emoteBubble)
    {
        Asset<Texture2D> asset;
        lock (Gate) asset = textures[emoteBubble.Type];
        return asset.Value;
    }

    internal static ModEmoteBubble? InstanceOf(EmoteBubble bubble)
    {
        if (bubble.IsNull || GetEmoteBubble(bubble.emote) is not ModEmoteBubble template) return null;
        lock (Gate)
        {
            if (instances.TryGetValue(bubble.Pointer, out ModEmoteBubble? current) && current.Type == template.Type) return current;
            ModEmoteBubble created = template.NewInstance(bubble);
            created.Type = template.Type;
            instances[bubble.Pointer] = created;
            return created;
        }
    }

    internal static void Forget(IntPtr bubble)
    {
        lock (Gate) instances.Remove(bubble);
    }

    public static void OnSpawn(EmoteBubble emoteBubble)
    {
        ModEmoteBubble? own = InstanceOf(emoteBubble);
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try { global.OnSpawn(emoteBubble); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.OnSpawn", e); }
        }
        if (own == null) return;
        try { own.OnSpawn(); }
        catch (Exception e) { ItemHooks.Report($"{own.FullName}.OnSpawn", e); }
    }

    public static bool UpdateFrame(EmoteBubble emoteBubble)
    {
        bool result = true;
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try { result &= global.UpdateFrame(emoteBubble); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.UpdateFrame", e); }
        }
        if (!result) return false;
        ModEmoteBubble? own = InstanceOf(emoteBubble);
        if (own == null) return true;
        try { return own.UpdateFrame(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own.FullName}.UpdateFrame", e);
            return true;
        }
    }

    public static bool PreDraw(EmoteBubble emoteBubble, SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects)
    {
        bool result = true;
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try { result &= global.PreDraw(emoteBubble, spriteBatch, texture, position, frame, origin, spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.PreDraw", e); }
        }
        if (!result) return false;
        ModEmoteBubble? own = InstanceOf(emoteBubble);
        if (own == null) return true;
        try { return own.PreDraw(spriteBatch, texture, position, frame, origin, spriteEffects); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own.FullName}.PreDraw", e);
            return true;
        }
    }

    public static void PostDraw(EmoteBubble emoteBubble, SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects)
    {
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try { global.PostDraw(emoteBubble, spriteBatch, texture, position, frame, origin, spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.PostDraw", e); }
        }
        ModEmoteBubble? own = InstanceOf(emoteBubble);
        if (own == null) return;
        try { own.PostDraw(spriteBatch, texture, position, frame, origin, spriteEffects); }
        catch (Exception e) { ItemHooks.Report($"{own.FullName}.PostDraw", e); }
    }

    public static Rectangle? GetFrame(EmoteBubble emoteBubble)
    {
        ModEmoteBubble? own = InstanceOf(emoteBubble);
        if (own != null)
        {
            try { return own.GetFrame(); }
            catch (Exception e)
            {
                ItemHooks.Report($"{own.FullName}.GetFrame", e);
                return null;
            }
        }
        Rectangle? result = null;
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try
            {
                if (global.GetFrame(emoteBubble) is Rectangle frame) result = frame;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.GetFrame", e); }
        }
        return result;
    }

    public static Rectangle? GetFrameInEmoteMenu(int emoteType, int frame, int frameCounter)
    {
        if (emoteType >= EmoteID.Count)
        {
            if (GetEmoteBubble(emoteType) is not ModEmoteBubble own) return null;
            try { return own.GetFrameInEmoteMenu(frame, frameCounter); }
            catch (Exception e)
            {
                ItemHooks.Report($"{own.FullName}.GetFrameInEmoteMenu", e);
                return null;
            }
        }
        Rectangle? result = null;
        foreach (GlobalEmoteBubble global in globalEmoteBubbles)
        {
            try
            {
                if (global.GetFrameInEmoteMenu(emoteType, frame, frameCounter) is Rectangle rectangle) result = rectangle;
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.GetFrameInEmoteMenu", e); }
        }
        return result;
    }
}
