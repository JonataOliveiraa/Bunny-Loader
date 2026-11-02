using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleLowLifePlayer : ModPlayer
{
    public const float Threshold = 0.25f;

    ScreenPresets.Tint? vignette;
    bool fading;

    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer)
            return;
        bool low = !Player.dead && Player.statLife < Player.statLifeMax2 * Threshold;
        if (low && vignette is not { Active: true })
        {
            fading = false;
            vignette = ScreenEffects.Spawn<ScreenPresets.Tint>(fx =>
            {
                fx.Color = new Color(200, 20, 20);
                fx.Blend = ScreenGraphics.BlendMode.Multiply;
                fx.Amount = 0.6f;
                fx.Saturation = 0.6f;
                fx.Mask = ScreenUtils.Mask.Vignette(0.35f, 0.55f);
                fx.Pulse = new ScreenPresets.TintPulse { Period = 50, Amount = 0.4f };
                fx.FadeIn = 20;
                fx.Key = "ExampleMod:lowLife";
            });
        }
        else if (!low && vignette is { Active: true } && !fading)
        {
            fading = true;
            ScreenPresets.Tint fx = vignette;
            fx.Tween(nameof(fx.Intensity), 0, 30, ScreenUtils.Easing.OutQuad, onDone: _ => fx.Kill());
        }
    }
}
