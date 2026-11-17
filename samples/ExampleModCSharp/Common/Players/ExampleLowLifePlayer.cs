using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

// This ModPlayer shows off ScreenPresets.Tint, one of the ready-made screen effects.
// While the player is low on life, the edges of the screen get a pulsing red vignette, which fades out once the player is healed.
public class ExampleLowLifePlayer : ModPlayer
{
    // The fraction of max life below which the vignette shows up. 0.25f is 25%.
    public const float Threshold = 0.25f;

    // The running effect, kept so we can fade it out later. ScreenEffects.Spawn returns the instance it creates.
    ScreenPresets.Tint? vignette;
    // Set while the vignette is fading out, so the fade only starts once.
    bool fading;

    public override void PostUpdate()
    {
        // Screen effects are only drawn for the local player, so other players' ModPlayer instances have nothing to do here.
        if (Player.whoAmI != Main.myPlayer)
            return;

        bool low = !Player.dead && Player.statLife < Player.statLifeMax2 * Threshold;
        if (low && vignette is not { Active: true })
        {
            fading = false;
            // The lambda runs once, before the effect is first drawn, and is where the effect's settings are chosen.
            vignette = ScreenEffects.Spawn<ScreenPresets.Tint>(fx =>
            {
                fx.Color = new Color(200, 20, 20);
                fx.Blend = ScreenGraphics.BlendMode.Multiply; // Multiply darkens the screen towards the color instead of painting over it.
                fx.Amount = 0.6f;
                fx.Saturation = 0.6f; // Washes out the colors a bit, which reads as "hurt".
                fx.Mask = ScreenUtils.Mask.Vignette(0.35f, 0.55f); // Only the edges of the screen are tinted. The values are where the vignette starts and where it reaches full strength.
                fx.Pulse = new ScreenPresets.TintPulse { Period = 50, Amount = 0.4f }; // A heartbeat-like pulse, every 50 ticks.
                fx.FadeIn = 20; // In ticks. 60 ticks is 1 second.
                fx.Key = "ExampleMod:lowLife"; // Spawning an effect whose Key is already active reuses that effect instead of stacking a second one.
            });
        }
        else if (!low && vignette is { Active: true } && !fading)
        {
            // Tween smoothly changes a property over time. Here Intensity goes to 0 over 30 ticks, and the effect is killed when it gets there.
            fading = true;
            ScreenPresets.Tint fx = vignette;
            fx.Tween(nameof(fx.Intensity), 0, 30, ScreenUtils.Easing.OutQuad, onDone: _ => fx.Kill());
        }
    }
}
