using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs;

public class ExampleModConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("Items")]
    [DefaultValue(true)]
    [ReloadRequired]
    public bool ExampleWingsToggle;

    [ReloadRequired]
    public bool WeaponWithGrowingDamageToggle;

    [Header("Other")]
    [DefaultValue(true)]
    [ReloadRequired]
    public bool WingSlotLoadoutSupportToggle;
}
