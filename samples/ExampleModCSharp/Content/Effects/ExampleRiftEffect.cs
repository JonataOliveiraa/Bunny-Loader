using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleMod.Content.Effects;

public class ExampleRiftEffect : ModScreenEffect
{
    public float Open { get; set; }

    public float Spin { get; set; } = 1.6f;

    public float Pull { get; set; } = 12;

    public override void SetDefaults()
    {
        Radius = 110;
        Color = new Color(180, 90, 255);
    }

    public override void OnSpawn() => Tween(nameof(Open), 1, 25, ScreenUtils.Easing.OutBack);

    public override void Update() => Uniforms["uOpen"] = Open;

    public override void DrawDistortion(DistortionMap d)
    {
        if (Center is not Vector2 center)
            return;
        d.Swirl(center, Radius, Spin * Open);
        d.Radial(center, Radius * 0.8f, -Pull * Open);
    }

    public void Close(int ticks = 12) => Tween(nameof(Open), 0, ticks, ScreenUtils.Easing.InQuad, onDone: _ => Kill());
}
