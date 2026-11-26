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

public interface IEffectLights
{
    Vector3 AmbientLightColor { get; set; }

    DirectionalLight DirectionalLight0 { get; }

    DirectionalLight DirectionalLight1 { get; }

    DirectionalLight DirectionalLight2 { get; }

    bool LightingEnabled { get; set; }

    void EnableDefaultLighting();
}

public interface IEffectFog
{
    bool FogEnabled { get; set; }

    float FogStart { get; set; }

    float FogEnd { get; set; }

    Vector3 FogColor { get; set; }
}

public sealed class DirectionalLight
{
    public Vector3 Direction { get; set; }

    public Vector3 DiffuseColor { get; set; }

    public Vector3 SpecularColor { get; set; }

    public bool Enabled { get; set; }

    public DirectionalLight(EffectParameter directionParameter, EffectParameter diffuseColorParameter, EffectParameter specularColorParameter, DirectionalLight? cloneSource)
    {
        if (cloneSource == null) return;
        Direction = cloneSource.Direction;
        DiffuseColor = cloneSource.DiffuseColor;
        SpecularColor = cloneSource.SpecularColor;
        Enabled = cloneSource.Enabled;
    }
}

public class BasicEffect : IEffectMatrices, IEffectLights, IEffectFog, IDisposable
{
    static bool warnedLighting, warnedFog;

    bool lightingEnabled, fogEnabled;
    static Effect shared;

    static BasicEffect? current;

    Matrix world = Matrix.Identity, view = Matrix.Identity, projection = Matrix.Identity, worldViewProjection = Matrix.Identity;

    bool dirty;

    static EffectTechnique technique;

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
        dirty = true;
        diffuseColor = cloneSource.diffuseColor;
        emissiveColor = cloneSource.emissiveColor;
        alpha = cloneSource.alpha;
        vertexColorEnabled = cloneSource.vertexColorEnabled;
        textureEnabled = cloneSource.textureEnabled;
        texture = cloneSource.texture;
        lightingEnabled = cloneSource.lightingEnabled;
        fogEnabled = cloneSource.fogEnabled;
        AmbientLightColor = cloneSource.AmbientLightColor;
        FogColor = cloneSource.FogColor;
        FogStart = cloneSource.FogStart;
        FogEnd = cloneSource.FogEnd;
        SpecularColor = cloneSource.SpecularColor;
        SpecularPower = cloneSource.SpecularPower;
        PreferPerPixelLighting = cloneSource.PreferPerPixelLighting;
        DirectionalLight0 = new(default, default, default, cloneSource.DirectionalLight0);
        DirectionalLight1 = new(default, default, default, cloneSource.DirectionalLight1);
        DirectionalLight2 = new(default, default, default, cloneSource.DirectionalLight2);
        current = this;
    }

    public virtual BasicEffect Clone() => new(this);

    public Matrix World { get => world; set { world = value; dirty = true; current = this; } }

    public Matrix View { get => view; set { view = value; dirty = true; current = this; } }

    public Matrix Projection { get => projection; set { projection = value; dirty = true; current = this; } }

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

    public DirectionalLight DirectionalLight0 { get; } = new(default, default, default, null) { Enabled = true };

    public DirectionalLight DirectionalLight1 { get; } = new(default, default, default, null);

    public DirectionalLight DirectionalLight2 { get; } = new(default, default, default, null);

    public bool LightingEnabled
    {
        get => lightingEnabled;
        set
        {
            lightingEnabled = value;
            if (!value || warnedLighting) return;
            warnedLighting = true;
            Log.Warn("BasicEffect: lighting is not drawn on mobile; the colors are drawn as with LightingEnabled = false");
        }
    }

    public bool FogEnabled
    {
        get => fogEnabled;
        set
        {
            fogEnabled = value;
            if (!value || warnedFog) return;
            warnedFog = true;
            Log.Warn("BasicEffect: fog is not drawn on mobile");
        }
    }

    public void EnableDefaultLighting()
    {
        LightingEnabled = true;
        AmbientLightColor = new Vector3(0.05333332f, 0.09882354f, 0.1819608f);
        DirectionalLight0.Direction = new Vector3(-0.5265408f, -0.5735765f, -0.6275069f);
        DirectionalLight0.DiffuseColor = new Vector3(1, 0.9607844f, 0.8078432f);
        DirectionalLight0.SpecularColor = new Vector3(1, 0.9607844f, 0.8078432f);
        DirectionalLight0.Enabled = true;
        DirectionalLight1.Direction = new Vector3(0.7198464f, 0.3420201f, 0.6040227f);
        DirectionalLight1.DiffuseColor = new Vector3(0.9647059f, 0.7607844f, 0.4078432f);
        DirectionalLight1.SpecularColor = Vector3.Zero;
        DirectionalLight1.Enabled = true;
        DirectionalLight2.Direction = new Vector3(0.4545195f, -0.7660444f, 0.4545195f);
        DirectionalLight2.DiffuseColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
        DirectionalLight2.SpecularColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
        DirectionalLight2.Enabled = true;
    }

    public EffectTechnique CurrentTechnique
    {
        get
        {
            current = this;
            if (technique.IsNull) technique = Shared.CurrentTechnique;
            return technique;
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

    internal ref readonly Matrix WorldViewProjection
    {
        get
        {
            if (dirty)
            {
                worldViewProjection = world * view * projection;
                dirty = false;
            }

            return ref worldViewProjection;
        }
    }

    internal Vector4 Tint => new((diffuseColor.X + emissiveColor.X) * alpha, (diffuseColor.Y + emissiveColor.Y) * alpha, (diffuseColor.Z + emissiveColor.Z) * alpha, alpha);
}
