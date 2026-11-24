using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace BunnyLoader;

public abstract class FxcScreenEffect : ModScreenEffect
{
    public FxcEffect? Effect { get; set; }

    public string PassName { get; set; } = "";

    public Vector3 SecondaryColor { get; set; } = Vector3.One;

    public float ShaderOpacity { get; set; } = 1;

    public Vector2 Direction { get; set; } = new(0, -1);

    public Vector2 ImageOffset { get; set; }

    public float? ShaderProgress { get; set; }

    public Dictionary<int, ScreenGraphics.Target> Images { get; } = new();

    protected FxcScreenEffect()
    {
        Fullscreen = true;
    }

    public virtual void SetParameters(FxcEffect effect) { }

    public override void Draw(ScreenCommands cmd)
    {
        if (Effect == null || PassName.Length == 0) return;
        Shader ??= Effect.Shader(PassName);
        FxcEffect e = Effect;
        Vector3 color = Color is Color c ? new Vector3(c.R / 255f, c.G / 255f, c.B / 255f) : Vector3.One;
        Vector2 screen = ScreenUtils.Space.Size;
        Vector2 target = Center ?? Main.screenPosition + screen / 2;
        e.SetValue("uColor", color)
            .SetValue("uSecondaryColor", SecondaryColor)
            .SetValue("uOpacity", ShaderOpacity * Opacity)
            .SetValue("uTime", Main.GlobalTimeWrappedHourly)
            .SetValue("uScreenResolution", screen)
            .SetValue("uScreenPosition", Main.screenPosition)
            .SetValue("uTargetPosition", target)
            .SetValue("uImageOffset", ImageOffset)
            .SetValue("uIntensity", Intensity)
            .SetValue("uProgress", ShaderProgress ?? Progress)
            .SetValue("uDirection", Direction)
            .SetValue("uZoom", Vector2.One)
            .SetValue("uImageSize0", screen);
        var options = new ScreenDrawOptions();
        foreach (var pair in Images)
        {
            options.Textures[FxcEffect.SamplerUniform(pair.Key)] = pair.Value;
            if (pair.Value is ScreenGraphics.Texture texture)
                e.SetValue("uImageSize" + pair.Key, new Vector2(texture.Width, texture.Height));
        }
        SetParameters(e);
        options.Uniforms["ps_uniforms_vec4"] = e.Commit(PassName);
        cmd.DrawShader(options);
    }
}
