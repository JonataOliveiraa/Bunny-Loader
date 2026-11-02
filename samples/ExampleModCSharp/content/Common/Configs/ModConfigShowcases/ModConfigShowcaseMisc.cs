using System.ComponentModel;
using ExampleMod.Common.Configs.CustomDataTypes;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases;

public class ModConfigShowcaseMisc : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("Colors")]
    [DefaultValue(typeof(Color), "255, 0, 0, 255"), ColorHSLSlider(false), ColorNoAlpha]
    public Color hsl;

    [Header("EnumOptions")]
    public SampleEnum EnumDefault;

    [Slider, DrawTicks]
    public SampleEnum EnumSlider;

    [Dropdown]
    public SampleEnum EnumDropdown;

    [Cycle]
    public SampleEnum EnumCycle;
}
