using ExampleMod.Common.Systems;
using ExampleMod.Content.Dusts;
using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

// This ModPlayer shows how to react to the touch buttons and the joystick registered in TouchButtonSystem.
// Touch buttons are read in ProcessTriggers, just like ModKeybinds.
public class ExampleTouchButtonPlayer : ModPlayer
{
    // How many ticks the hold button needs to be held to reach 100%. 60 ticks is 1 second.
    const int FullCharge = 120;
    // The speed of the projectile shot by the aim joystick, in pixels per tick.
    const float AimSpeed = 12f;

    // How long the hold button has been held, in ticks.
    public int HoldCharge;
    public static LocalizedText TouchText { get; private set; }
    public static LocalizedText HoldText { get; private set; }

    public override void SetStaticDefaults()
    {
        TouchText = Mod.GetLocalization($"{nameof(ExampleTouchButtonPlayer)}.Touch");
        HoldText = Mod.GetLocalization($"{nameof(ExampleTouchButtonPlayer)}.Hold");
    }

    // OnRelease runs when the player lets go of the joystick, with the last direction it was pushed in.
    public override void OnEnterWorld() => TouchButtonSystem.ExampleAimJoystick.OnRelease = Shoot;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        // JustPressed is only true on the tick the button was touched.
        if (TouchButtonSystem.ExampleTouchButton.JustPressed)
            Main.NewText(TouchText.Value);

        // Current is true for as long as the button is held down, and JustReleased only on the tick it was let go.
        if (TouchButtonSystem.ExampleHoldButton.Current)
        {
            if (HoldCharge < FullCharge) HoldCharge++;
            Dust.NewDust(Player.position, Player.width, Player.height, ModContent.DustType<Sparkle>());
        }
        else if (TouchButtonSystem.ExampleHoldButton.JustReleased)
        {
            Main.NewText(HoldText.Format(HoldCharge * 100 / FullCharge));
            HoldCharge = 0;
        }

        // The animation of a touch button keeps running on its own. Tapping the animated button stops its loading circle, and tapping it again starts it back.
        if (TouchButtonSystem.ExampleAnimatedButton.JustPressed)
            TouchButtonSystem.LoadingAnimation.NotActuallyAnimating = !TouchButtonSystem.LoadingAnimation.NotActuallyAnimating;

        // While the joystick is being pushed, show where the shot will go with a dust in front of the player.
        Vector2 aim = TouchButtonSystem.ExampleAimJoystick.Direction;
        if (TouchButtonSystem.ExampleAimJoystick.Aiming && aim != Vector2.Zero)
        {
            Dust dust = Dust.NewDustPerfect(Player.Center + aim.SafeNormalize(Vector2.UnitX) * 48f, ModContent.DustType<Sparkle>(), Vector2.Zero);
            dust.noGravity = true;
        }
    }

    static void Shoot(Player player, Vector2 direction)
    {
        Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, direction.SafeNormalize(Vector2.UnitX) * AimSpeed, ModContent.ProjectileType<SparklingBall>(), 20, 2f, player.whoAmI);
    }
}
