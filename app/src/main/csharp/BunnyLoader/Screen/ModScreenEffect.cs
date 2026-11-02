using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace BunnyLoader;

[Autoload(Side = ModSide.Client)]
public abstract class ModScreenEffect : ModType
{
    bool killed;

    internal Func<Vector2?>? follow;

    public ScreenGraphics.Shader? Shader { get; set; }

    public ScreenEffects.Layer Layer { get; set; }

    public Vector2? Center { get; set; }

    public float Radius { get; set; } = 64;

    public float Intensity { get; set; } = 1;

    public Color? Color { get; set; }

    public float Duration { get; set; }

    public float FadeIn { get; set; }

    public float FadeOut { get; set; }

    public string? Key { get; set; }

    public int Order { get; set; }

    public float CaptureMargin { get; set; } = 8;

    public bool Fullscreen { get; set; }

    public ScreenUtils.Mask? Mask { get; set; }

    public Dictionary<string, object> Uniforms { get; } = new();

    public float Age { get; set; }

    public bool Ended { get; internal set; }

    public bool Following => follow != null;

    public virtual string? Source => null;

    public virtual string ShaderPath => (GetType().Namespace + "." + Name).Replace('.', '/');

    public bool Active => !killed;

    public float Progress => Duration > 0 ? MathF.Min(Age / Duration, 1) : 0;

    public float TimeLeft => Duration > 0 ? MathF.Max(Duration - Age, 0) : float.PositiveInfinity;

    public float Opacity
    {
        get
        {
            float opacity = 1;
            if (FadeIn > 0) opacity *= MathF.Min(Age / FadeIn, 1);
            if (Duration > 0 && FadeOut > 0) opacity *= MathF.Min(TimeLeft / FadeOut, 1);
            return opacity;
        }
    }

    protected sealed override void Register() => ScreenEffects.Register(this);

    public virtual void SetDefaults() { }

    public virtual void OnSpawn() { }

    public virtual void Update() { }

    public virtual bool ShouldDraw() => true;

    public virtual void DrawDistortion(DistortionMap d) { }

    public virtual void Draw(ScreenCommands cmd)
    {
        if (Shader != null) cmd.DrawShader();
    }

    public virtual void OnKill() { }

    public void Kill() => killed = true;

    public ScreenUtils.Tween Tween(string property, float to, float duration, Func<float, float>? easing = null, float delay = 0, Action<object>? onDone = null) =>
        ScreenUtils.Tween.To(this, property, to, duration, easing, delay, onDone);
}
