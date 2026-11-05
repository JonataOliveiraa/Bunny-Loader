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

public class ExampleTouchButtonPlayer : ModPlayer
{
    const int FullCharge = 120;
    const float AimSpeed = 12f;

    public int HoldCharge;
    public static LocalizedText TouchText { get; private set; }
    public static LocalizedText HoldText { get; private set; }

    public override void SetStaticDefaults()
    {
        TouchText = Mod.GetLocalization($"{nameof(ExampleTouchButtonPlayer)}.Touch");
        HoldText = Mod.GetLocalization($"{nameof(ExampleTouchButtonPlayer)}.Hold");
    }

    public override void OnEnterWorld() => TouchButtonSystem.ExampleAimJoystick.OnRelease = Shoot;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (TouchButtonSystem.ExampleTouchButton.JustPressed)
            Main.NewText(TouchText.Value);

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
