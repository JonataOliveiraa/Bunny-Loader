using System.IO;

namespace Microsoft.Xna.Framework.Graphics;

public readonly unsafe partial struct Texture2D
{
    public Texture2D(GraphicsDevice graphicsDevice, int width, int height)
        : this(TextureData.Create(width, height, SurfaceFormat.Color))
    {
    }

    public Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipMap, SurfaceFormat format)
        : this(TextureData.Create(width, height, format))
    {
    }

    public static Texture2D FromStream(GraphicsDevice graphicsDevice, Stream stream) => new(TextureData.FromStream(stream));

    public void SaveAsPng(Stream stream, int width, int height) => PngWriter.Write(stream, this, width, height);

    public void SetData<T>(T[] data) where T : unmanaged =>
        TextureData.Write(this, 0, null, data, 0, data?.Length ?? 0);

    public void SetData<T>(T[] data, int startIndex, int elementCount) where T : unmanaged =>
        TextureData.Write(this, 0, null, data, startIndex, elementCount);

    public void SetData<T>(int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : unmanaged =>
        TextureData.Write(this, level, rect, data, startIndex, elementCount);

    public void GetData<T>(T[] data) where T : unmanaged =>
        TextureData.Read(this, 0, null, data, 0, data?.Length ?? 0);

    public void GetData<T>(T[] data, int startIndex, int elementCount) where T : unmanaged =>
        TextureData.Read(this, 0, null, data, startIndex, elementCount);

    public void GetData<T>(int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : unmanaged =>
        TextureData.Read(this, level, rect, data, startIndex, elementCount);
}
