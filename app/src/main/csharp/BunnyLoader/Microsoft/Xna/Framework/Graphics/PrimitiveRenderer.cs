using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria;
using Terraria.GameContent;

namespace Microsoft.Xna.Framework.Graphics;

internal static unsafe class PrimitiveRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    struct GameVertex
    {
        public float X, Y;
        public uint Color;
        public float U, V;
    }

    const int MaxVertices = 32766;

    static readonly GameClass VertexClass = new("Microsoft.Xna.Framework.Graphics", "SpriteBatcher", "VertexData");

    static readonly GameClass ShortClass = new("System", "Int16");

    static readonly string[] MatrixNames = { "worldviewprojection", "wvp", "mvp", "matrixtransform", "transformmatrix", "transform", "matrix" };

    static readonly Dictionary<nint, EffectParameter> matrixParameters = new();

    static GameArray<GameVertex> vertexPool;

    static GameArray<short> indexPool;

    static uint vertexHandle, indexHandle;

    static GameVertex[] converted = new GameVertex[256];

    static int[] triangles = new int[384];

    static int[] sequence = new int[384];

    struct Layout
    {
        public int Position, Color, TexCoord;
        public VertexElementFormat PositionFormat, TexCoordFormat;
    }

    struct State
    {
        public Matrix Transform;
        public Vector4 Tint;
        public bool VertexColor;
        public Texture2D Texture;
        public float Width, Height;
    }

    static Texture2D white;

    static Texture2D White => white.IsNull ? white = TextureAssets.MagicPixel.Value : white;

    public static void Draw(ReadOnlySpan<byte> vertices, int stride, VertexDeclaration declaration, PrimitiveType type, int primitiveCount,
        ReadOnlySpan<byte> indices, int indexSize, int baseVertex)
    {
        if (primitiveCount <= 0) return;
        if (declaration == null) throw new ArgumentNullException(nameof(declaration));
        if (type == PrimitiveType.PointListEXT) throw new NotSupportedException("GraphicsDevice: point lists are not supported on mobile");

        State state = CurrentState();
        Layout layout = Read(declaration);
        int available = vertices.Length / stride;
        int indexCount = IndexCount(type, primitiveCount);
        if (!indices.IsEmpty && indices.Length < indexCount * indexSize)
            throw new ArgumentOutOfRangeException(nameof(indices), $"GraphicsDevice: {primitiveCount} primitives need {indexCount} indices, {indices.Length / indexSize} given");
        Grow(ref sequence, indexCount);
        int low = int.MaxValue, high = -1;

        for (int i = 0; i < indexCount; i++)
        {
            int index = (indices.IsEmpty ? i : indexSize == 2 ? MemoryMarshal.Read<short>(indices.Slice(i * 2)) : MemoryMarshal.Read<int>(indices.Slice(i * 4))) + baseVertex;
            if ((uint)index >= (uint)available) throw new ArgumentOutOfRangeException(nameof(indices), $"GraphicsDevice: vertex {index} is outside the {available} vertices given");
            sequence[i] = index;
            if (index < low) low = index;
            if (index > high) high = index;
        }

        int count = high - low + 1;
        Grow(ref converted, count);

        fixed (byte* source = vertices)
            for (int i = 0; i < count; i++)
                converted[i] = Convert(source + (low + i) * stride, layout, state);

        if (low != 0)
            for (int i = 0; i < indexCount; i++) sequence[i] -= low;

        int written = Expand(type, primitiveCount);
        if (type is PrimitiveType.LineList or PrimitiveType.LineStrip) written = Lines(ref count, written);

        Submit(count, written, state.Texture);
    }

    static int IndexCount(PrimitiveType type, int primitives) => type switch
    {
        PrimitiveType.TriangleList => primitives * 3,
        PrimitiveType.TriangleStrip => primitives + 2,
        PrimitiveType.LineList => primitives * 2,
        PrimitiveType.LineStrip => primitives + 1,
        _ => throw new NotSupportedException($"GraphicsDevice: {type} is not supported on mobile"),
    };

    static int Expand(PrimitiveType type, int primitives)
    {
        bool lines = type is PrimitiveType.LineList or PrimitiveType.LineStrip;
        Grow(ref triangles, lines ? primitives * 6 : primitives * 3);
        int written = 0;

        for (int k = 0; k < primitives; k++)
        {
            switch (type)
            {
                case PrimitiveType.TriangleList:
                    triangles[written++] = sequence[k * 3];
                    triangles[written++] = sequence[k * 3 + 1];
                    triangles[written++] = sequence[k * 3 + 2];
                    break;
                case PrimitiveType.TriangleStrip:
                    bool even = (k & 1) == 0;
                    triangles[written++] = sequence[even ? k : k + 1];
                    triangles[written++] = sequence[even ? k + 1 : k];
                    triangles[written++] = sequence[k + 2];
                    break;
                case PrimitiveType.LineList:
                    triangles[written++] = sequence[k * 2];
                    triangles[written++] = sequence[k * 2 + 1];
                    break;
                case PrimitiveType.LineStrip:
                    triangles[written++] = sequence[k];
                    triangles[written++] = sequence[k + 1];
                    break;
            }
        }

        return written;
    }

    static int Lines(ref int count, int written)
    {
        int segments = written / 2;
        Grow(ref converted, count + segments * 4);
        Grow(ref sequence, written);
        Array.Copy(triangles, sequence, written);
        int output = 0;

        for (int s = 0; s < segments; s++)
        {
            GameVertex a = converted[sequence[s * 2]], b = converted[sequence[s * 2 + 1]];
            float dx = b.X - a.X, dy = b.Y - a.Y, length = MathF.Sqrt(dx * dx + dy * dy);
            if (length < 1e-4f) continue;
            float nx = -dy / length * 0.5f, ny = dx / length * 0.5f;
            int first = count;
            converted[count++] = Offset(a, nx, ny);
            converted[count++] = Offset(a, -nx, -ny);
            converted[count++] = Offset(b, nx, ny);
            converted[count++] = Offset(b, -nx, -ny);
            Span<int> quad = stackalloc int[] { first, first + 1, first + 2, first + 2, first + 1, first + 3 };
            foreach (int q in quad) triangles[output++] = q;
        }

        return output;
    }

    static GameVertex Offset(GameVertex v, float x, float y)
    {
        v.X += x;
        v.Y += y;
        return v;
    }

    static void Submit(int count, int written, Texture2D texture)
    {
        if (written == 0) return;
        GraphicsDevice device = GraphicsDevice.Instance;
        GameArray<Texture2D> textures = device.Textures;
        if (!texture.IsNull && !textures.IsNull && textures.Length > 0 && textures[0].Pointer != texture.Pointer) textures[0] = texture;

        if (count <= MaxVertices)
        {
            Reserve(count, written);
            Span<GameVertex> v = vertexPool.AsSpan();
            Span<short> ix = indexPool.AsSpan();
            converted.AsSpan(0, count).CopyTo(v);
            for (int i = 0; i < written; i++) ix[i] = (short)triangles[i];
            device.DrawGeometry(new GameObject(vertexPool.Pointer), indexPool, count, written, Vector3.Zero, Vector3.One);
            return;
        }

        int chunk = MaxVertices / 3 * 3;
        Reserve(chunk, chunk);

        for (int start = 0; start < written; start += chunk)
        {
            int n = Math.Min(chunk, written - start);
            Span<GameVertex> v = vertexPool.AsSpan();
            Span<short> ix = indexPool.AsSpan();

            for (int i = 0; i < n; i++)
            {
                v[i] = converted[triangles[start + i]];
                ix[i] = (short)i;
            }

            device.DrawGeometry(new GameObject(vertexPool.Pointer), indexPool, n, n, Vector3.Zero, Vector3.One);
        }
    }

    static void Reserve(int vertices, int indices)
    {
        if (vertexPool.IsNull || vertexPool.Length < vertices)
        {
            if (vertexHandle != 0) Host.Native()->GcHandleFree(vertexHandle);
            vertexPool = GameArray<GameVertex>.New(VertexClass, Math.Max(vertices, 1024));
            vertexHandle = Host.Native()->GcHandleNew(vertexPool.Pointer, 0);
        }

        if (indexPool.IsNull || indexPool.Length < indices)
        {
            if (indexHandle != 0) Host.Native()->GcHandleFree(indexHandle);
            indexPool = GameArray<short>.New(ShortClass, Math.Max(indices, 1536));
            indexHandle = Host.Native()->GcHandleNew(indexPool.Pointer, 0);
        }
    }

    static void Grow<T>(ref T[] array, int size)
    {
        if (array.Length < size) Array.Resize(ref array, Math.Max(size, array.Length * 2));
    }

    static State CurrentState()
    {
        EffectPass pass = EffectPass.BoundEffect;
        if (pass.IsNull)
            throw new InvalidOperationException("GraphicsDevice: apply an effect pass before drawing primitives (a BasicEffect or a mod's .fxc effect)");

        Effect effect = pass._effect;

        if (BasicEffect.IsShared(effect))
        {
            BasicEffect basic = BasicEffect.Current ?? throw new InvalidOperationException("GraphicsDevice: the BasicEffect that was applied is gone");
            return new State
            {
                Transform = basic.WorldViewProjection,
                Tint = basic.Tint,
                VertexColor = basic.VertexColorEnabled,
                Texture = basic.TextureEnabled && !basic.Texture.IsNull ? basic.Texture : White,
                Width = Main.screenWidth,
                Height = Main.screenHeight,
            };
        }

        return new State { Transform = MatrixOf(effect), Tint = Vector4.One, VertexColor = true, Width = Main.screenWidth, Height = Main.screenHeight };
    }

    static Matrix MatrixOf(Effect effect)
    {
        EffectParameter parameter;

        lock (matrixParameters)
        {
            if (!matrixParameters.TryGetValue(effect.Pointer, out parameter))
            {
                string? name = FindMatrix(FxcGameEffect.SourceOf(effect));
                parameter = name == null ? default : effect.Parameters[name];
                matrixParameters[effect.Pointer] = parameter;
            }
        }

        return !parameter.IsNull && parameter.TryGetMatrix(out Matrix value) ? value : Main.GameViewMatrix.NormalizedTransformationMatrix;
    }

    static string? FindMatrix(FxcEffect? fx)
    {
        if (fx == null) return null;
        var found = new List<string>();
        foreach (FxcEffect.Parameter p in fx.Parameters)
            if (p.Rows == 4 && p.Columns == 4 && p.Elements <= 1) found.Add(p.Name);
        if (found.Count <= 1) return found.Count == 1 ? found[0] : null;

        foreach (string wanted in MatrixNames)
            foreach (string name in found)
                if (name.ToLowerInvariant().Replace("_", "").TrimStart('u').Contains(wanted)) return name;

        Log.Warn($"fxc: {fx.Name} has {found.Count} matrices; primitives use {found[0]} as the world-view-projection");
        return found[0];
    }

    static Layout Read(VertexDeclaration declaration)
    {
        var layout = new Layout { Position = -1, Color = -1, TexCoord = -1 };

        foreach (VertexElement e in declaration.Elements)
        {
            if (e.UsageIndex != 0) continue;

            switch (e.VertexElementUsage)
            {
                case VertexElementUsage.Position when e.VertexElementFormat is VertexElementFormat.Vector2 or VertexElementFormat.Vector3 or VertexElementFormat.Vector4:
                    layout.Position = e.Offset;
                    layout.PositionFormat = e.VertexElementFormat;
                    break;
                case VertexElementUsage.Color when e.VertexElementFormat == VertexElementFormat.Color:
                    layout.Color = e.Offset;
                    break;
                case VertexElementUsage.TextureCoordinate when e.VertexElementFormat is VertexElementFormat.Vector2 or VertexElementFormat.Vector3 or VertexElementFormat.Vector4:
                    layout.TexCoord = e.Offset;
                    layout.TexCoordFormat = e.VertexElementFormat;
                    break;
            }
        }

        if (layout.Position < 0) throw new NotSupportedException("GraphicsDevice: the vertex declaration has no Vector2, Vector3 or Vector4 position");
        return layout;
    }

    static GameVertex Convert(byte* v, in Layout layout, in State state)
    {
        float* p = (float*)(v + layout.Position);
        float x = p[0], y = p[1];
        float z = layout.PositionFormat == VertexElementFormat.Vector2 ? 0f : p[2];
        float w = layout.PositionFormat == VertexElementFormat.Vector4 ? p[3] : 1f;
        ref readonly Matrix m = ref state.Transform;

        float cx = x * m.M11 + y * m.M21 + z * m.M31 + w * m.M41;
        float cy = x * m.M12 + y * m.M22 + z * m.M32 + w * m.M42;
        float cw = x * m.M14 + y * m.M24 + z * m.M34 + w * m.M44;
        if (cw == 0f) cw = 1e-6f;

        float r = 1f, g = 1f, b = 1f, a = 1f;

        if (layout.Color >= 0 && state.VertexColor)
        {
            Color c = *(Color*)(v + layout.Color);
            r = c.R / 255f;
            g = c.G / 255f;
            b = c.B / 255f;
            a = c.A / 255f;
        }

        float u = 0f, t = 0f;

        if (layout.TexCoord >= 0)
        {
            float* uv = (float*)(v + layout.TexCoord);
            u = uv[0];
            t = uv[1];
        }

        return new GameVertex
        {
            X = (cx / cw + 1f) * 0.5f * state.Width,
            Y = (1f - cy / cw) * 0.5f * state.Height,
            Color = Pack(r * state.Tint.X, g * state.Tint.Y, b * state.Tint.Z, a * state.Tint.W),
            U = u,
            V = 1f - t,
        };
    }

    static uint Pack(float r, float g, float b, float a) =>
        (uint)Math.Clamp((int)(r * 255f + 0.5f), 0, 255) | (uint)Math.Clamp((int)(g * 255f + 0.5f), 0, 255) << 8 |
        (uint)Math.Clamp((int)(b * 255f + 0.5f), 0, 255) << 16 | (uint)Math.Clamp((int)(a * 255f + 0.5f), 0, 255) << 24;
}
