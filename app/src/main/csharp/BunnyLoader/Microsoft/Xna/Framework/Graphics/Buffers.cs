using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Graphics;

public class VertexBuffer : IDisposable
{
    internal byte[] Data;

    public GraphicsDevice GraphicsDevice { get; }

    public VertexDeclaration VertexDeclaration { get; }

    public int VertexCount { get; }

    public BufferUsage BufferUsage { get; }

    public string Name { get; set; } = "";

    public object? Tag { get; set; }

    public bool IsDisposed { get; private set; }

    public VertexBuffer(GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration, int vertexCount, BufferUsage bufferUsage)
    {
        if (vertexDeclaration == null) throw new ArgumentNullException(nameof(vertexDeclaration));
        if (vertexCount <= 0) throw new ArgumentOutOfRangeException(nameof(vertexCount), "VertexBuffer: vertexCount must be positive");
        GraphicsDevice = graphicsDevice;
        VertexDeclaration = vertexDeclaration;
        VertexCount = vertexCount;
        BufferUsage = bufferUsage;
        Data = new byte[vertexDeclaration.VertexStride * vertexCount];
    }

    public VertexBuffer(GraphicsDevice graphicsDevice, Type vertexType, int vertexCount, BufferUsage bufferUsage)
        : this(graphicsDevice, VertexDeclaration.FromType(vertexType), vertexCount, bufferUsage)
    {
    }

    public void SetData<T>(T[] data) where T : struct => SetData(0, data, 0, data?.Length ?? 0, Unsafe.SizeOf<T>());

    public void SetData<T>(T[] data, int startIndex, int elementCount) where T : struct => SetData(0, data, startIndex, elementCount, Unsafe.SizeOf<T>());

    public void SetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount, int vertexStride) where T : struct =>
        Copy(data, startIndex, elementCount, vertexStride, offsetInBytes, true);

    public void GetData<T>(T[] data) where T : struct => GetData(0, data, 0, data?.Length ?? 0, Unsafe.SizeOf<T>());

    public void GetData<T>(T[] data, int startIndex, int elementCount) where T : struct => GetData(0, data, startIndex, elementCount, Unsafe.SizeOf<T>());

    public void GetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount, int vertexStride) where T : struct =>
        Copy(data, startIndex, elementCount, vertexStride, offsetInBytes, false);

    internal void Copy<T>(T[] data, int startIndex, int elementCount, int stride, int offsetInBytes, bool write) where T : struct
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(VertexBuffer));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (startIndex < 0 || elementCount < 0 || startIndex + elementCount > data.Length)
            throw new ArgumentOutOfRangeException(nameof(elementCount), $"VertexBuffer: {elementCount} elements from {startIndex} do not fit an array of {data.Length}");

        int size = Unsafe.SizeOf<T>();
        if (stride == 0) stride = size;
        if (stride < size) throw new ArgumentOutOfRangeException(nameof(stride), $"VertexBuffer: a stride of {stride} is smaller than {typeof(T).Name} ({size} bytes)");
        if (elementCount == 0) return;

        int end = offsetInBytes + (elementCount - 1) * stride + size;
        if (offsetInBytes < 0 || end > Data.Length)
            throw new ArgumentOutOfRangeException(nameof(offsetInBytes), $"VertexBuffer: {end} bytes do not fit a buffer of {Data.Length}");

        Span<byte> source = MemoryMarshal.AsBytes(data.AsSpan(startIndex, elementCount));

        for (int i = 0; i < elementCount; i++)
        {
            Span<byte> element = source.Slice(i * size, size);
            Span<byte> slot = Data.AsSpan(offsetInBytes + i * stride, size);
            if (write) element.CopyTo(slot);
            else slot.CopyTo(element);
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public class DynamicVertexBuffer : VertexBuffer
{
    public bool IsContentLost => false;

#pragma warning disable CS0067
    public event EventHandler<EventArgs>? ContentLost;
#pragma warning restore CS0067

    public DynamicVertexBuffer(GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration, int vertexCount, BufferUsage bufferUsage)
        : base(graphicsDevice, vertexDeclaration, vertexCount, bufferUsage)
    {
    }

    public DynamicVertexBuffer(GraphicsDevice graphicsDevice, Type vertexType, int vertexCount, BufferUsage bufferUsage)
        : base(graphicsDevice, vertexType, vertexCount, bufferUsage)
    {
    }

    public void SetData<T>(T[] data, int startIndex, int elementCount, SetDataOptions options) where T : struct =>
        SetData(0, data, startIndex, elementCount, Unsafe.SizeOf<T>());

    public void SetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount, int vertexStride, SetDataOptions options) where T : struct =>
        SetData(offsetInBytes, data, startIndex, elementCount, vertexStride);
}

public class IndexBuffer : IDisposable
{
    internal byte[] Data;

    public GraphicsDevice GraphicsDevice { get; }

    public IndexElementSize IndexElementSize { get; }

    public int IndexCount { get; }

    public BufferUsage BufferUsage { get; }

    public string Name { get; set; } = "";

    public object? Tag { get; set; }

    public bool IsDisposed { get; private set; }

    public IndexBuffer(GraphicsDevice graphicsDevice, IndexElementSize indexElementSize, int indexCount, BufferUsage bufferUsage)
    {
        if (indexCount <= 0) throw new ArgumentOutOfRangeException(nameof(indexCount), "IndexBuffer: indexCount must be positive");
        GraphicsDevice = graphicsDevice;
        IndexElementSize = indexElementSize;
        IndexCount = indexCount;
        BufferUsage = bufferUsage;
        Data = new byte[indexCount * (indexElementSize == IndexElementSize.SixteenBits ? 2 : 4)];
    }

    public IndexBuffer(GraphicsDevice graphicsDevice, Type indexType, int indexCount, BufferUsage bufferUsage)
        : this(graphicsDevice, SizeOf(indexType), indexCount, bufferUsage)
    {
    }

    static IndexElementSize SizeOf(Type indexType) =>
        indexType == typeof(short) || indexType == typeof(ushort) ? IndexElementSize.SixteenBits :
        indexType == typeof(int) || indexType == typeof(uint) ? IndexElementSize.ThirtyTwoBits :
        throw new NotSupportedException($"IndexBuffer: indices are short, ushort, int or uint, not {indexType?.Name}");

    public void SetData<T>(T[] data) where T : struct => SetData(0, data, 0, data?.Length ?? 0);

    public void SetData<T>(T[] data, int startIndex, int elementCount) where T : struct => SetData(0, data, startIndex, elementCount);

    public void SetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount) where T : struct => Copy(offsetInBytes, data, startIndex, elementCount, true);

    public void GetData<T>(T[] data) where T : struct => GetData(0, data, 0, data?.Length ?? 0);

    public void GetData<T>(T[] data, int startIndex, int elementCount) where T : struct => GetData(0, data, startIndex, elementCount);

    public void GetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount) where T : struct => Copy(offsetInBytes, data, startIndex, elementCount, false);

    void Copy<T>(int offsetInBytes, T[] data, int startIndex, int elementCount, bool write) where T : struct
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(IndexBuffer));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (startIndex < 0 || elementCount < 0 || startIndex + elementCount > data.Length)
            throw new ArgumentOutOfRangeException(nameof(elementCount), $"IndexBuffer: {elementCount} elements from {startIndex} do not fit an array of {data.Length}");

        Span<byte> source = MemoryMarshal.AsBytes(data.AsSpan(startIndex, elementCount));
        if (offsetInBytes < 0 || offsetInBytes + source.Length > Data.Length)
            throw new ArgumentOutOfRangeException(nameof(offsetInBytes), $"IndexBuffer: {offsetInBytes + source.Length} bytes do not fit a buffer of {Data.Length}");

        if (write) source.CopyTo(Data.AsSpan(offsetInBytes));
        else Data.AsSpan(offsetInBytes, source.Length).CopyTo(source);
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public class DynamicIndexBuffer : IndexBuffer
{
    public bool IsContentLost => false;

#pragma warning disable CS0067
    public event EventHandler<EventArgs>? ContentLost;
#pragma warning restore CS0067

    public DynamicIndexBuffer(GraphicsDevice graphicsDevice, IndexElementSize indexElementSize, int indexCount, BufferUsage usage)
        : base(graphicsDevice, indexElementSize, indexCount, usage)
    {
    }

    public DynamicIndexBuffer(GraphicsDevice graphicsDevice, Type indexType, int indexCount, BufferUsage usage)
        : base(graphicsDevice, indexType, indexCount, usage)
    {
    }

    public void SetData<T>(int offsetInBytes, T[] data, int startIndex, int elementCount, SetDataOptions options) where T : struct =>
        SetData(offsetInBytes, data, startIndex, elementCount);

    public void SetData<T>(T[] data, int startIndex, int elementCount, SetDataOptions options) where T : struct =>
        SetData(0, data, startIndex, elementCount);
}
