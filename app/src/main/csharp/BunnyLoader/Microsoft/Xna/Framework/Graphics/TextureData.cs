using System;
using System.IO;
using BunnyLoader;

namespace Microsoft.Xna.Framework.Graphics;

internal static unsafe class TextureData
{
    enum Result
    {
        Ok = 0,
        NoTexture = 1,
        NotOwned = 2,
        NotReadable = 3,
        BadRegion = 4,
        UnsupportedFormat = 5,
        Failed = 6,
    }

    enum PixelOrder
    {
        Rgba = 0,
        Abgr = 1,
    }

    const int BytesPerPixel = 4;

    static int OrderOf<T>() where T : unmanaged => (int)(typeof(T) == typeof(Color) ? PixelOrder.Abgr : PixelOrder.Rgba);

    public static IntPtr Create(int width, int height, SurfaceFormat format)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "a texture needs a positive size");
        if (format != SurfaceFormat.Color) throw new NotSupportedException($"Texture2D: only SurfaceFormat.Color can be created on mobile, not {format}");
        RequireGameThread("new Texture2D");

        IntPtr texture = Host.Native()->TextureCreate(width, height);
        if (texture == IntPtr.Zero) throw new InvalidOperationException($"Texture2D: the game could not create a {width}x{height} texture");
        return texture;
    }

    public static IntPtr FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        RequireGameThread("Texture2D.FromStream");

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        byte[] bytes = copy.ToArray();

        IntPtr texture;
        fixed (byte* data = bytes) texture = Host.Native()->TextureFromPng(data, bytes.Length);
        if (texture == IntPtr.Zero) throw new InvalidDataException("Texture2D.FromStream: the stream is not a PNG image the game can read");
        return texture;
    }

    public static void Write<T>(Texture2D texture, int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : unmanaged
    {
        Rectangle area = Prepare(texture, "SetData", level, rect, data, startIndex, elementCount);
        int result;
        fixed (T* start = &data[startIndex])
            result = Host.Native()->TextureWrite(texture.Pointer, area.X, area.Y, area.Width, area.Height, (byte*)start, OrderOf<T>());
        Check((Result)result, "SetData");
    }

    public static void Read<T>(Texture2D texture, int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : unmanaged
    {
        Rectangle area = Prepare(texture, "GetData", level, rect, data, startIndex, elementCount);
        int result;
        fixed (T* start = &data[startIndex])
            result = Host.Native()->TextureRead(texture.Pointer, area.X, area.Y, area.Width, area.Height, (byte*)start, OrderOf<T>());
        Check((Result)result, "GetData");
    }

    static Rectangle Prepare<T>(Texture2D texture, string method, int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(data);
        if (texture.IsNull) throw new NullReferenceException($"Texture2D.{method}: the texture is null");
        if (level != 0) throw new NotSupportedException($"Texture2D.{method}: only level 0 exists on mobile");
        if (startIndex < 0 || elementCount <= 0 || startIndex > data.Length - elementCount)
            throw new ArgumentOutOfRangeException(nameof(elementCount), $"Texture2D.{method}: {elementCount} elements from index {startIndex} do not fit an array of {data.Length}");
        RequireGameThread($"Texture2D.{method}");

        Rectangle area = rect ?? new Rectangle(0, 0, texture.Width, texture.Height);
        long bytes = (long)elementCount * sizeof(T);
        long needed = (long)area.Width * area.Height * BytesPerPixel;
        if (bytes != needed)
            throw new ArgumentException($"Texture2D.{method}: {elementCount} x {sizeof(T)} bytes do not match a {area.Width}x{area.Height} region ({needed} bytes)");
        return area;
    }

    static void Check(Result result, string method)
    {
        switch (result)
        {
            case Result.Ok:
                return;
            case Result.NoTexture:
                throw new NullReferenceException($"Texture2D.{method}: the texture is null");
            case Result.NotOwned:
                throw new NotSupportedException($"Texture2D.{method}: this texture is a game atlas, palette or render target. On mobile only textures that hold their own pixels work: new Texture2D, Texture2D.FromStream and a mod's own images");
            case Result.NotReadable:
                throw new NotSupportedException($"Texture2D.{method}: the pixels of this texture are only on the GPU (a game or texture pack image)");
            case Result.BadRegion:
                throw new ArgumentOutOfRangeException("rect", $"Texture2D.{method}: the region is outside the texture");
            case Result.UnsupportedFormat:
                throw new NotSupportedException($"Texture2D.{method}: the texture is not a 32-bit color texture");
            default:
                throw new InvalidOperationException($"Texture2D.{method}: the game failed to access the pixels");
        }
    }

    static void RequireGameThread(string what)
    {
        if (Host.Native()->IsGameThread() == 0)
            throw new InvalidOperationException($"{what} only works on the game thread (SetStaticDefaults, hooks, drawing), not while the mod loads");
    }
}
