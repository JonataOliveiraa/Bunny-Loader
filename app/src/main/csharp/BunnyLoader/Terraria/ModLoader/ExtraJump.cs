using System.Collections.Generic;

namespace Terraria.ModLoader;

public abstract class ExtraJump : ModType
{
    public static ExtraJump Flipper { get; private set; } = new FlipperJump();

    public static ExtraJump BasiliskMount { get; private set; } = new BasiliskMountJump();

    public static ExtraJump GoatMount { get; private set; } = new GoatMountJump();

    public static ExtraJump SantankMount { get; private set; } = new SantankMountJump();

    public static ExtraJump UnicornMount { get; private set; } = new UnicornMountJump();

    public static ExtraJump SandstormInABottle { get; private set; } = new SandstormInABottleJump();

    public static ExtraJump BlizzardInABottle { get; private set; } = new BlizzardInABottleJump();

    public static ExtraJump FartInAJar { get; private set; } = new FartInAJarJump();

    public static ExtraJump TsunamiInABottle { get; private set; } = new TsunamiInABottleJump();

    public static ExtraJump CloudInABottle { get; private set; } = new CloudInABottleJump();

    public abstract class Position
    {
        public ExtraJump Target { get; protected init; } = null!;
    }

    public sealed class Before : Position
    {
        public Before(ExtraJump parent) => Target = parent;
    }

    public sealed class After : Position
    {
        public After(ExtraJump parent) => Target = parent;
    }

    protected static readonly Position BeforeMountJumps = new Before(BasiliskMount);
    protected static readonly Position MountJumpPosition = new After(BasiliskMount);
    protected static readonly Position BeforeBottleJumps = new Before(SandstormInABottle);
    protected static readonly Position AfterBottleJumps = new After(CloudInABottle);

    public int Type { get; internal set; }

    protected sealed override void Register() => Type = ExtraJumpLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public override string ToString() => Name;

    public abstract Position GetDefaultPosition();

    public virtual IEnumerable<Position> GetModdedConstraints() => null!;

    public virtual void ShowVisuals(Player player) { }

    public virtual bool CanShowVisuals(Player player) =>
        (player.gravDir == 1f && player.velocity.Y < 0f) || (player.gravDir == -1f && player.velocity.Y > 0f);

    public abstract float GetDurationMultiplier(Player player);

    public virtual bool CanStart(Player player) => true;

    public virtual void OnStarted(Player player, ref bool playSound) { }

    public virtual void OnEnded(Player player) { }

    public virtual void UpdateHorizontalSpeeds(Player player) { }

    public virtual void OnRefreshed(Player player) { }
}
