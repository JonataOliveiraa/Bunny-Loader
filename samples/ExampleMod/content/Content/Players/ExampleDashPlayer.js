const { DustID } = Terraria.ID;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleDashPlayer extends ModPlayer {
    static DashDown = 0;
    static DashUp = 1;
    static DashRight = 2;
    static DashLeft = 3;

    DashCooldown = 50;
    DashDuration = 35;
    DashVelocity = 10;

    DashAccessoryEquipped = false;
    DashDir = -1;
    DashDelay = 0;
    DashTimer = 0;

    ResetEffects(player) {
        this.DashAccessoryEquipped = false;

        const timer = player.doubleTapCardinalTimer;
        if (player.controlDown && player.releaseDown && timer[0] < 15) {
            this.DashDir = ExampleDashPlayer.DashDown;
        } else if (player.controlUp && player.releaseUp && timer[1] < 15) {
            this.DashDir = ExampleDashPlayer.DashUp;
        } else if (player.controlRight && player.releaseRight && timer[2] < 15 && timer[3] === 0) {
            this.DashDir = ExampleDashPlayer.DashRight;
        } else if (player.controlLeft && player.releaseLeft && timer[3] < 15 && timer[2] === 0) {
            this.DashDir = ExampleDashPlayer.DashLeft;
        } else {
            this.DashDir = -1;
        }
    }

    CanUseDash(player) {
        return this.DashAccessoryEquipped
            && player.dashType === 0
            && !player.setSolar
            && !player.mount.Active;
    }

    OnStartDash(player) {
        for (let i = 0; i < 20; i++) {
            NewDust(player.position, player.width, player.height, DustID.Smoke, 0, 0, 100, Color.White, 1.5);
        }
    }

    UpdateDash(player, dashTime) {
        player.eocDash = dashTime;
        player.armorEffectDrawShadowEOCShield = true;
    }

    ResetDash(player) {
        player.eocDash = 0;
        player.armorEffectDrawShadowEOCShield = false;
    }

    UpdateMovement(player) {
        if (this.CanUseDash(player) && this.DashDir !== -1 && this.DashDelay === 0) {
            const velocity = player.velocity;
            switch (this.DashDir) {
                case ExampleDashPlayer.DashUp:
                    if (velocity.Y > -this.DashVelocity) velocity.Y = -1.3 * this.DashVelocity;
                    break;
                case ExampleDashPlayer.DashDown:
                    if (velocity.Y < this.DashVelocity) velocity.Y = this.DashVelocity;
                    break;
                case ExampleDashPlayer.DashLeft:
                    if (velocity.X > -this.DashVelocity) velocity.X = -this.DashVelocity;
                    break;
                case ExampleDashPlayer.DashRight:
                    if (velocity.X < this.DashVelocity) velocity.X = this.DashVelocity;
                    break;
            }
            this.DashDelay = this.DashCooldown;
            this.DashTimer = this.DashDuration;
            this.OnStartDash(player);
        }

        if (this.DashDelay > 0) this.DashDelay--;

        if (this.DashTimer > 0) {
            this.UpdateDash(player, this.DashTimer);
            this.DashTimer--;
            if (this.DashTimer === 0) this.ResetDash(player);
        }
    }
}
