using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleMod.Content.Effects;

// A ModScreenEffect is a screen effect with its own shader. This one draws a swirling rift, used by ExampleRiftProjectile.
// The shader is loaded from ExampleRiftEffect.frag, next to this file: by default ShaderPath is the class's namespace and name.
public class ExampleRiftEffect : ModScreenEffect
{
    // How open the rift is, from 0 (closed) to 1 (fully open). It is animated with Tween and passed to the shader as uOpen.
    public float Open { get; set; }

    // How much the screen is twisted around the rift, in radians.
    public float Spin { get; set; } = 1.6f;

    // How strongly the screen is pulled towards the center of the rift, in pixels.
    public float Pull { get; set; } = 12;

    // SetDefaults runs once for every spawned effect, before the callback passed to ScreenEffects.Spawn.
    public override void SetDefaults()
    {
        Radius = 110;
        Color = new Color(180, 90, 255);
    }

    // Opens the rift over 25 ticks. OutBack overshoots a little before settling, which makes the opening feel snappy.
    public override void OnSpawn() => Tween(nameof(Open), 1, 25, ScreenUtils.Easing.OutBack);

    // Uniforms are passed to the shader every frame. The keys must match the uniform names in the .frag file.
    public override void Update() => Uniforms["uOpen"] = Open;

    // DrawDistortion bends the picture of the game behind the effect. Each call adds one distortion shape to the map.
    public override void DrawDistortion(DistortionMap d)
    {
        // Center is null when the effect was spawned without a position.
        if (Center is not Vector2 center)
            return;

        // Both distortions scale with Open, so the rift grows and shrinks smoothly. A negative Radial strength pulls inwards.
        d.Swirl(center, Radius, Spin * Open);
        d.Radial(center, Radius * 0.8f, -Pull * Open);
    }

    // Closes the rift and then removes the effect.
    public void Close(int ticks = 12) => Tween(nameof(Open), 0, ticks, ScreenUtils.Easing.InQuad, onDone: _ => Kill());
}
