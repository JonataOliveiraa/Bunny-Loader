using System;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Graphics;

public readonly unsafe partial struct GraphicsDevice
{
    static IndexBuffer? indices;

    static VertexBufferBinding[] vertexBuffers = Array.Empty<VertexBufferBinding>();

    public IndexBuffer Indices
    {
        get => indices!;
        set => indices = value;
    }

    public void SetVertexBuffer(VertexBuffer vertexBuffer) => SetVertexBuffer(vertexBuffer, 0);

    public void SetVertexBuffer(VertexBuffer vertexBuffer, int vertexOffset) =>
        vertexBuffers = vertexBuffer == null ? Array.Empty<VertexBufferBinding>() : new[] { new VertexBufferBinding(vertexBuffer, vertexOffset) };

    public void SetVertexBuffers(params VertexBufferBinding[] vertexBuffers)
    {
        if (vertexBuffers != null && vertexBuffers.Length > 1)
            throw new NotSupportedException("GraphicsDevice: only one vertex buffer can be bound on mobile");
        GraphicsDevice.vertexBuffers = vertexBuffers == null ? Array.Empty<VertexBufferBinding>() : (VertexBufferBinding[])vertexBuffers.Clone();
    }

    public VertexBufferBinding[] GetVertexBuffers() => (VertexBufferBinding[])vertexBuffers.Clone();

    public void DrawPrimitives(PrimitiveType primitiveType, int vertexStart, int primitiveCount)
    {
        VertexBuffer buffer = Bound();
        int stride = buffer.VertexDeclaration.VertexStride;
        int first = (vertexBuffers[0].VertexOffset + vertexStart) * stride;
        int length = PrimitiveVerts(primitiveType, primitiveCount) * stride;
        if (first < 0 || first + length > buffer.Data.Length)
            throw new ArgumentOutOfRangeException(nameof(primitiveCount), $"GraphicsDevice: {primitiveCount} primitives from vertex {vertexStart} do not fit the {buffer.VertexCount} vertices of the buffer");
        PrimitiveRenderer.Draw(buffer.Data.AsSpan(first, length), stride, buffer.VertexDeclaration, primitiveType, primitiveCount, default, 0, 0);
    }

    public void DrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount)
    {
        VertexBuffer buffer = Bound();
        IndexBuffer index = indices ?? throw new InvalidOperationException("GraphicsDevice: set Indices before DrawIndexedPrimitives");
        int stride = buffer.VertexDeclaration.VertexStride;
        int size = index.IndexElementSize == IndexElementSize.SixteenBits ? 2 : 4;
        PrimitiveRenderer.Draw(buffer.Data.AsSpan(vertexBuffers[0].VertexOffset * stride), stride, buffer.VertexDeclaration, primitiveType, primitiveCount,
            index.Data.AsSpan(startIndex * size), size, baseVertex);
    }

    public void DrawInstancedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount, int instanceCount) =>
        throw new NotSupportedException("GraphicsDevice: instanced drawing is not supported on mobile");

    public void DrawUserPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int primitiveCount) where T : struct, IVertexType =>
        DrawUserPrimitives(primitiveType, vertexData, vertexOffset, primitiveCount, Declaration<T>());

    public void DrawUserPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct
    {
        if (vertexData == null) throw new ArgumentNullException(nameof(vertexData));
        int stride = StrideOf(vertexDeclaration);
        PrimitiveRenderer.Draw(Range(vertexData, vertexOffset, PrimitiveVerts(primitiveType, primitiveCount), stride), stride, vertexDeclaration, primitiveType, primitiveCount, default, 0, 0);
    }

    public void DrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, short[] indexData, int indexOffset, int primitiveCount) where T : struct, IVertexType =>
        DrawUserIndexedPrimitives(primitiveType, vertexData, vertexOffset, numVertices, indexData, indexOffset, primitiveCount, Declaration<T>());

    public void DrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, short[] indexData, int indexOffset, int primitiveCount,
        VertexDeclaration vertexDeclaration) where T : struct
    {
        if (vertexData == null) throw new ArgumentNullException(nameof(vertexData));
        if (indexData == null) throw new ArgumentNullException(nameof(indexData));
        int stride = StrideOf(vertexDeclaration);
        PrimitiveRenderer.Draw(Range(vertexData, vertexOffset, numVertices, stride), stride, vertexDeclaration, primitiveType, primitiveCount,
            MemoryMarshal.AsBytes(indexData.AsSpan(indexOffset)), 2, 0);
    }

    public void DrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, int[] indexData, int indexOffset, int primitiveCount) where T : struct, IVertexType =>
        DrawUserIndexedPrimitives(primitiveType, vertexData, vertexOffset, numVertices, indexData, indexOffset, primitiveCount, Declaration<T>());

    public void DrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, int[] indexData, int indexOffset, int primitiveCount,
        VertexDeclaration vertexDeclaration) where T : struct
    {
        if (vertexData == null) throw new ArgumentNullException(nameof(vertexData));
        if (indexData == null) throw new ArgumentNullException(nameof(indexData));
        int stride = StrideOf(vertexDeclaration);
        PrimitiveRenderer.Draw(Range(vertexData, vertexOffset, numVertices, stride), stride, vertexDeclaration, primitiveType, primitiveCount,
            MemoryMarshal.AsBytes(indexData.AsSpan(indexOffset)), 4, 0);
    }

    static VertexBuffer Bound()
    {
        if (vertexBuffers.Length == 0 || vertexBuffers[0].VertexBuffer == null)
            throw new InvalidOperationException("GraphicsDevice: set a vertex buffer before DrawPrimitives");
        VertexBuffer buffer = vertexBuffers[0].VertexBuffer;
        if (buffer.IsDisposed) throw new ObjectDisposedException(nameof(VertexBuffer));
        return buffer;
    }

    static VertexDeclaration Declaration<T>() where T : struct, IVertexType => default(T).VertexDeclaration;

    static int StrideOf(VertexDeclaration declaration)
    {
        if (declaration == null) throw new ArgumentNullException(nameof(declaration));
        return declaration.VertexStride;
    }

    static int PrimitiveVerts(PrimitiveType type, int primitives) => type switch
    {
        PrimitiveType.TriangleList => primitives * 3,
        PrimitiveType.TriangleStrip => primitives + 2,
        PrimitiveType.LineList => primitives * 2,
        PrimitiveType.LineStrip => primitives + 1,
        _ => primitives,
    };

    static ReadOnlySpan<byte> Range<T>(T[] data, int vertexOffset, int vertices, int stride) where T : struct
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data.AsSpan());
        long start = (long)vertexOffset * stride, length = (long)vertices * stride;
        if (vertexOffset < 0 || vertices < 0 || start + length > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(vertices), $"GraphicsDevice: {vertices} vertices from {vertexOffset} do not fit {typeof(T).Name}[{data.Length}] with a stride of {stride}");
        return bytes.Slice((int)start, (int)length);
    }
}
