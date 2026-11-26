using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Graphics;

public enum PrimitiveType
{
    TriangleList,
    TriangleStrip,
    LineList,
    LineStrip,
    PointListEXT,
}

public enum BufferUsage
{
    None,
    WriteOnly,
}

public enum IndexElementSize
{
    SixteenBits,
    ThirtyTwoBits,
}

public enum SetDataOptions
{
    None,
    Discard,
    NoOverwrite,
}

public enum VertexElementFormat
{
    Single,
    Vector2,
    Vector3,
    Vector4,
    Color,
    Byte4,
    Short2,
    Short4,
    NormalizedShort2,
    NormalizedShort4,
    HalfVector2,
    HalfVector4,
}

public enum VertexElementUsage
{
    Position,
    Color,
    TextureCoordinate,
    Normal,
    Binormal,
    Tangent,
    BlendIndices,
    BlendWeight,
    Depth,
    Fog,
    PointSize,
    Sample,
    TessellateFactor,
}

public struct VertexElement : IEquatable<VertexElement>
{
    public int Offset { get; set; }

    public VertexElementFormat VertexElementFormat { get; set; }

    public VertexElementUsage VertexElementUsage { get; set; }

    public int UsageIndex { get; set; }

    public VertexElement(int offset, VertexElementFormat elementFormat, VertexElementUsage elementUsage, int usageIndex)
    {
        Offset = offset;
        VertexElementFormat = elementFormat;
        VertexElementUsage = elementUsage;
        UsageIndex = usageIndex;
    }

    public static int SizeOf(VertexElementFormat format) => format switch
    {
        VertexElementFormat.Single => 4,
        VertexElementFormat.Vector2 => 8,
        VertexElementFormat.Vector3 => 12,
        VertexElementFormat.Vector4 => 16,
        VertexElementFormat.Color => 4,
        VertexElementFormat.Byte4 => 4,
        VertexElementFormat.Short2 => 4,
        VertexElementFormat.Short4 => 8,
        VertexElementFormat.NormalizedShort2 => 4,
        VertexElementFormat.NormalizedShort4 => 8,
        VertexElementFormat.HalfVector2 => 4,
        VertexElementFormat.HalfVector4 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public readonly bool Equals(VertexElement other) =>
        Offset == other.Offset && VertexElementFormat == other.VertexElementFormat && VertexElementUsage == other.VertexElementUsage && UsageIndex == other.UsageIndex;

    public override readonly bool Equals(object? obj) => obj is VertexElement e && Equals(e);

    public override readonly int GetHashCode() => HashCode.Combine(Offset, VertexElementFormat, VertexElementUsage, UsageIndex);

    public override readonly string ToString() =>
        $"{{Offset:{Offset} Format:{VertexElementFormat} Usage:{VertexElementUsage} UsageIndex:{UsageIndex}}}";

    public static bool operator ==(VertexElement a, VertexElement b) => a.Equals(b);

    public static bool operator !=(VertexElement a, VertexElement b) => !a.Equals(b);
}

public class VertexDeclaration : IDisposable
{
    readonly VertexElement[] elements;

    public int VertexStride { get; }

    public string Name { get; set; } = "";

    public object? Tag { get; set; }

    public bool IsDisposed { get; private set; }

    public VertexDeclaration(params VertexElement[] elements)
        : this(elements.Length == 0 ? 0 : elements.Max(e => e.Offset + VertexElement.SizeOf(e.VertexElementFormat)), elements)
    {
    }

    public VertexDeclaration(int vertexStride, params VertexElement[] elements)
    {
        if (elements == null || elements.Length == 0) throw new ArgumentNullException(nameof(elements), "VertexDeclaration: elements cannot be empty");
        this.elements = (VertexElement[])elements.Clone();
        VertexStride = vertexStride;
    }

    public VertexElement[] GetVertexElements() => (VertexElement[])elements.Clone();

    internal ReadOnlySpan<VertexElement> Elements => elements;

    public static VertexDeclaration FromType(Type vertexType)
    {
        if (vertexType == null) throw new ArgumentNullException(nameof(vertexType));
        if (!vertexType.IsValueType || !typeof(IVertexType).IsAssignableFrom(vertexType))
            throw new ArgumentException($"VertexDeclaration: {vertexType.Name} is not a struct that implements IVertexType", nameof(vertexType));

        var instance = (IVertexType)Activator.CreateInstance(vertexType)!;
        return instance.VertexDeclaration ?? throw new ArgumentException($"VertexDeclaration: {vertexType.Name}.VertexDeclaration is null");
    }

    public void Dispose() => IsDisposed = true;
}

public interface IVertexType
{
    VertexDeclaration VertexDeclaration { get; }
}

public struct VertexBufferBinding
{
    public static readonly VertexBufferBinding None = new(null!);

    public VertexBuffer VertexBuffer { get; }

    public int VertexOffset { get; }

    public int InstanceFrequency { get; }

    public VertexBufferBinding(VertexBuffer vertexBuffer) : this(vertexBuffer, 0, 0) { }

    public VertexBufferBinding(VertexBuffer vertexBuffer, int vertexOffset) : this(vertexBuffer, vertexOffset, 0) { }

    public VertexBufferBinding(VertexBuffer vertexBuffer, int vertexOffset, int instanceFrequency)
    {
        VertexBuffer = vertexBuffer;
        VertexOffset = vertexOffset;
        InstanceFrequency = instanceFrequency;
    }

    public static implicit operator VertexBufferBinding(VertexBuffer buffer) => new(buffer);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionColor : IVertexType, IEquatable<VertexPositionColor>
{
    public Vector3 Position;

    public Color Color;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionColor(Vector3 position, Color color)
    {
        Position = position;
        Color = color;
    }

    public readonly bool Equals(VertexPositionColor other) => Position.Equals(other.Position) && Color.Equals(other.Color);

    public override readonly bool Equals(object? obj) => obj is VertexPositionColor v && Equals(v);

    public override readonly int GetHashCode() => HashCode.Combine(Position, Color);

    public override readonly string ToString() => $"{{Position:{Position} Color:{Color}}}";

    public static bool operator ==(VertexPositionColor a, VertexPositionColor b) => a.Equals(b);

    public static bool operator !=(VertexPositionColor a, VertexPositionColor b) => !a.Equals(b);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionColorTexture : IVertexType, IEquatable<VertexPositionColorTexture>
{
    public Vector3 Position;

    public Color Color;

    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionColorTexture(Vector3 position, Color color, Vector2 textureCoordinate)
    {
        Position = position;
        Color = color;
        TextureCoordinate = textureCoordinate;
    }

    public readonly bool Equals(VertexPositionColorTexture other) =>
        Position.Equals(other.Position) && Color.Equals(other.Color) && TextureCoordinate.Equals(other.TextureCoordinate);

    public override readonly bool Equals(object? obj) => obj is VertexPositionColorTexture v && Equals(v);

    public override readonly int GetHashCode() => HashCode.Combine(Position, Color, TextureCoordinate);

    public override readonly string ToString() => $"{{Position:{Position} Color:{Color} TextureCoordinate:{TextureCoordinate}}}";

    public static bool operator ==(VertexPositionColorTexture a, VertexPositionColorTexture b) => a.Equals(b);

    public static bool operator !=(VertexPositionColorTexture a, VertexPositionColorTexture b) => !a.Equals(b);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionTexture : IVertexType, IEquatable<VertexPositionTexture>
{
    public Vector3 Position;

    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionTexture(Vector3 position, Vector2 textureCoordinate)
    {
        Position = position;
        TextureCoordinate = textureCoordinate;
    }

    public readonly bool Equals(VertexPositionTexture other) => Position.Equals(other.Position) && TextureCoordinate.Equals(other.TextureCoordinate);

    public override readonly bool Equals(object? obj) => obj is VertexPositionTexture v && Equals(v);

    public override readonly int GetHashCode() => HashCode.Combine(Position, TextureCoordinate);

    public override readonly string ToString() => $"{{Position:{Position} TextureCoordinate:{TextureCoordinate}}}";

    public static bool operator ==(VertexPositionTexture a, VertexPositionTexture b) => a.Equals(b);

    public static bool operator !=(VertexPositionTexture a, VertexPositionTexture b) => !a.Equals(b);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionNormalTexture : IVertexType, IEquatable<VertexPositionNormalTexture>
{
    public Vector3 Position;

    public Vector3 Normal;

    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
    }

    public readonly bool Equals(VertexPositionNormalTexture other) =>
        Position.Equals(other.Position) && Normal.Equals(other.Normal) && TextureCoordinate.Equals(other.TextureCoordinate);

    public override readonly bool Equals(object? obj) => obj is VertexPositionNormalTexture v && Equals(v);

    public override readonly int GetHashCode() => HashCode.Combine(Position, Normal, TextureCoordinate);

    public override readonly string ToString() => $"{{Position:{Position} Normal:{Normal} TextureCoordinate:{TextureCoordinate}}}";

    public static bool operator ==(VertexPositionNormalTexture a, VertexPositionNormalTexture b) => a.Equals(b);

    public static bool operator !=(VertexPositionNormalTexture a, VertexPositionNormalTexture b) => !a.Equals(b);
}
