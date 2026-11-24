using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;

namespace BunnyLoader;

public abstract class FxcScreenEffect : ModScreenEffect
{
    FxcEffect? effect;
    string passName = "";
    FxcEffect.Pass? pass;
    int version = -1;
    float[] registers = new float[4];
    ScreenGraphics.UniformArray? uniforms;
    readonly ScreenDrawOptions options = new();

    public FxcEffect? Effect
    {
        get => effect;
        set
        {
            effect = value;
            Resolve();
        }
    }

    public string PassName
    {
        get => passName;
        set
        {
            passName = value ?? "";
            Resolve();
        }
    }

    public Vector3 SecondaryColor { get; set; } = Vector3.One;

    public float ShaderOpacity { get; set; } = 1;

    public Vector2 Direction { get; set; } = new(0, 1);

    public Vector2 ImageOffset { get; set; }

    public float? ShaderProgress { get; set; }

    public Dictionary<int, ScreenGraphics.Target> Images { get; } = new();

    protected FxcScreenEffect()
    {
        Fullscreen = true;
    }

    void Resolve()
    {
        pass = null;
        if (effect == null || passName.Length == 0) return;
        pass = effect.FindPass(passName);
        Shader = effect.ShaderOf(pass);
        version = effect.Version;
        int floats = System.Math.Max(pass.Registers, 1) * 4;
        if (registers.Length != floats) registers = new float[floats];
        uniforms = new ScreenGraphics.UniformArray(4, registers);
    }

    public virtual void SetParameters(FxcEffect effect) { }

    public override void Draw(ScreenCommands cmd)
    {
        if (effect == null || passName.Length == 0) return;
        if (pass == null || version != effect.Version) Resolve();
        FxcEffect e = effect;
        Vector3 color = Color is Color c ? new Vector3(c.R / 255f, c.G / 255f, c.B / 255f) : Vector3.One;
        Vector2 screen = ScreenShaderData.UnscaledScreenSize;
        Vector2 position = ScreenShaderData.UnscaledScreenPosition;
        Vector2 target = Center ?? position + screen / 2;
        e.SetValue("uColor", color)
            .SetValue("uSecondaryColor", SecondaryColor)
            .SetValue("uOpacity", ShaderOpacity * Opacity)
            .SetValue("uTime", Main.GlobalTimeWrappedHourly)
            .SetValue("uScreenResolution", screen)
            .SetValue("uScreenPosition", position)
            .SetValue("uTargetPosition", target)
            .SetValue("uImageOffset", ImageOffset)
            .SetValue("uSceneSize", screen)
            .SetValue("uSceneOffset", Vector2.Zero)
            .SetValue("uIntensity", Intensity)
            .SetValue("uProgress", ShaderProgress ?? Progress)
            .SetValue("uDirection", Direction)
            .SetValue("uZoom", Main.GameViewMatrix.RenderZoom)
            .SetValue("uMultiChunkScene", false)
            .SetValue("uImageSize0", screen);
        options.Textures.Clear();
        foreach (var pair in Images)
        {
            options.Textures[FxcEffect.SamplerUniform(pair.Key)] = pair.Value;
            if (pair.Value is ScreenGraphics.Texture texture)
                e.SetValue("uImageSize" + pair.Key, new Vector2(texture.Width, texture.Height));
        }
        SetParameters(e);
        e.Commit(pass!, registers);
        options.Uniforms["ps_uniforms_vec4"] = uniforms!;
        cmd.DrawShader(options);
    }
}
