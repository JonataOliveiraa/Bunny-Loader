using System;
using System.IO;
using BunnyLoader;

namespace Microsoft.Xna.Framework.Graphics;

public interface IEffectMatrices
{
    Matrix World { get; set; }

    Matrix View { get; set; }

    Matrix Projection { get; set; }
}

public class BasicEffect : IEffectMatrices, IDisposable
{
    static Effect shared;

    static BasicEffect? current;

    Matrix world = Matrix.Identity, view = Matrix.Identity, projection = Matrix.Identity;

    Vector3 diffuseColor = Vector3.One, emissiveColor;

    float alpha = 1f;

    bool vertexColorEnabled, textureEnabled;

    Texture2D texture;

    public GraphicsDevice GraphicsDevice { get; }

    public string Name { get; set; } = "BasicEffect";

    public object? Tag { get; set; }

    public bool IsDisposed { get; private set; }

    public BasicEffect(GraphicsDevice device)
    {
        GraphicsDevice = device;
        current = this;
    }

    protected BasicEffect(BasicEffect cloneSource)
    {
        GraphicsDevice = cloneSource.GraphicsDevice;
        world = cloneSource.world;
        view = cloneSource.view;
        projection = cloneSource.projection;
        diffuseColor = cloneSource.diffuseColor;
        emissiveColor = cloneSource.emissiveColor;
        alpha = cloneSource.alpha;
        vertexColorEnabled = cloneSource.vertexColorEnabled;
        textureEnabled = cloneSource.textureEnabled;
        texture = cloneSource.texture;
        current = this;
    }

    public virtual BasicEffect Clone() => new(this);

    public Matrix World { get => world; set { world = value; current = this; } }

    public Matrix View { get => view; set { view = value; current = this; } }

    public Matrix Projection { get => projection; set { projection = value; current = this; } }

    public Vector3 DiffuseColor { get => diffuseColor; set { diffuseColor = value; current = this; } }

    public Vector3 EmissiveColor { get => emissiveColor; set { emissiveColor = value; current = this; } }

    public Vector3 SpecularColor { get; set; } = Vector3.One;

    public float SpecularPower { get; set; } = 16f;

    public float Alpha { get => alpha; set { alpha = value; current = this; } }

    public bool VertexColorEnabled { get => vertexColorEnabled; set { vertexColorEnabled = value; current = this; } }

    public bool TextureEnabled { get => textureEnabled; set { textureEnabled = value; current = this; } }

    public Texture2D Texture { get => texture; set { texture = value; current = this; } }

    public bool PreferPerPixelLighting { get; set; }

    public Vector3 AmbientLightColor { get; set; }

    public Vector3 FogColor { get; set; }

    public float FogStart { get; set; }

    public float FogEnd { get; set; } = 1f;

    public bool LightingEnabled
    {
        get => false;
        set { if (value) throw new NotSupportedException("BasicEffect: lighting is not supported on mobile; draw with vertex colors"); }
    }

    public bool FogEnabled
    {
        get => false;
        set { if (value) throw new NotSupportedException("BasicEffect: fog is not supported on mobile"); }
    }

    public void EnableDefaultLighting() => LightingEnabled = true;

    public EffectTechnique CurrentTechnique
    {
        get
        {
            current = this;
            return Shared.CurrentTechnique;
        }
        set => current = this;
    }

    public EffectTechniqueCollection Techniques
    {
        get
        {
            current = this;
            return Shared.Techniques;
        }
    }

    public EffectParameterCollection Parameters => Shared.Parameters;

    public static implicit operator Effect(BasicEffect effect)
    {
        current = effect;
        return Shared;
    }

    public void Dispose()
    {
        IsDisposed = true;
        if (current == this) current = null;
        GC.SuppressFinalize(this);
    }

    internal static Effect Shared
    {
        get
        {
            if (!shared.IsNull) return shared;
            using Stream stream = typeof(BasicEffect).Assembly.GetManifestResourceStream("BunnyLoader.BasicEffect.fxc")
                ?? throw new InvalidOperationException("BasicEffect: the embedded shader is missing from BunnyLoader.dll");
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            shared = FxcGameEffect.Create(FxcEffect.FromBytes(bytes, "BasicEffect"));
            return shared;
        }
    }

    internal static bool IsShared(Effect effect) => !shared.IsNull && effect.Pointer == shared.Pointer;

    internal static BasicEffect? Current => current;

    internal Matrix WorldViewProjection => world * view * projection;

    internal Vector4 Tint => new((diffuseColor.X + emissiveColor.X) * alpha, (diffuseColor.Y + emissiveColor.Y) * alpha, (diffuseColor.Z + emissiveColor.Z) * alpha, alpha);
}
