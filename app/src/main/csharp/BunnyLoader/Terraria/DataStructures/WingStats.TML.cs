namespace Terraria.DataStructures;

public partial struct WingStats
{
    public static readonly WingStats Default;

    public WingStats(int flyTime = 100, float flySpeedOverride = -1f, float accelerationMultiplier = 1f, bool hasHoldDownHoverFeatures = false,
                     float hoverFlySpeedOverride = -1f, float hoverAccelerationMultiplier = 1f)
    {
        FlyTime = flyTime;
        AccRunSpeedOverride = flySpeedOverride;
        AccRunAccelerationMult = accelerationMultiplier;
        HasDownHoverStats = hasHoldDownHoverFeatures;
        DownHoverSpeedOverride = hoverFlySpeedOverride;
        DownHoverAccelerationMult = hoverAccelerationMultiplier;
    }
}
