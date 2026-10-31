using System;
using System.Collections;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.DataStructures;

public readonly partial struct PlayerDrawSet
{
    public DrawDataList DrawDataCache => new(this);
}

public readonly struct DrawDataList : IEnumerable<DrawData>
{
    readonly PlayerDrawSet drawInfo;

    internal DrawDataList(PlayerDrawSet drawInfo) => this.drawInfo = drawInfo;

    public int Count => drawInfo.DrawDataCacheCount;

    public ref DrawData this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return ref drawInfo.DrawDataCache_[index];
        }
    }

    void Room(int count)
    {
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        if (count <= cache.Length) return;
        drawInfo.DrawDataCache_ = cache.Resized(Math.Max(count, cache.Length * 2));
    }

    public void Add(DrawData item)
    {
        int count = Count;
        Room(count + 1);
        drawInfo.DrawDataCache_[count] = item;
        drawInfo.DrawDataCacheCount = count + 1;
    }

    public void Insert(int index, DrawData item)
    {
        int count = Count;
        if ((uint)index > (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        Room(count + 1);
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        for (int i = count; i > index; i--) cache[i] = cache[i - 1];
        cache[index] = item;
        drawInfo.DrawDataCacheCount = count + 1;
    }

    public void RemoveAt(int index)
    {
        int count = Count;
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        for (int i = index; i < count - 1; i++) cache[i] = cache[i + 1];
        drawInfo.DrawDataCacheCount = count - 1;
    }

    public int RemoveAll(Predicate<DrawData> match)
    {
        int count = Count, kept = 0;
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        for (int i = 0; i < count; i++)
        {
            if (match(cache[i])) continue;
            if (kept != i) cache[kept] = cache[i];
            kept++;
        }
        drawInfo.DrawDataCacheCount = kept;
        return count - kept;
    }

    public int FindIndex(Predicate<DrawData> match)
    {
        int count = Count;
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        for (int i = 0; i < count; i++)
        {
            if (match(cache[i])) return i;
        }
        return -1;
    }

    public void Clear() => drawInfo.DrawDataCacheCount = 0;

    public IEnumerator<DrawData> GetEnumerator()
    {
        int count = Count;
        GameArray<DrawData> cache = drawInfo.DrawDataCache_;
        for (int i = 0; i < count; i++) yield return cache[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public partial struct DrawData
{
    public DrawData(Texture2D texture, Vector2 position, Color color) => this = New(texture, position, color);

    public DrawData(Texture2D texture, Vector2 position, Rectangle? sourceRect, Color color) =>
        this = sourceRect is { } rect ? New(texture, position, rect, color, 0f, default, 1f, SpriteEffects.None, 0) : New(texture, position, color);

    public DrawData(Texture2D texture, Vector2 position, Rectangle? sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, float inactiveLayerDepth = 0f) =>
        this = sourceRect is { } rect
            ? New(texture, position, rect, color, rotation, origin, scale, effect, (int)inactiveLayerDepth)
            : New(texture, position, color, rotation, origin, scale, effect, (int)inactiveLayerDepth);

    public DrawData(Texture2D texture, Vector2 position, Rectangle? sourceRect, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float inactiveLayerDepth = 0f) =>
        this = sourceRect is { } rect
            ? New(texture, position, rect, color, rotation, origin, scale, effect, (int)inactiveLayerDepth)
            : New(texture, position, color, rotation, origin, scale, effect, (int)inactiveLayerDepth);
}
