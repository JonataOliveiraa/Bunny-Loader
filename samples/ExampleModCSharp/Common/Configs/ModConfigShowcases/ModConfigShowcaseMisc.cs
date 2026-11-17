using System.ComponentModel;
using ExampleMod.Common.Configs.CustomDataTypes;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Config;

// This file contains fake ModConfig class that showcase various attributes
// that can be used to customize behavior config fields.

// Because this config was designed to show off various UI capabilities,
// this config have no effect on the mod and provides purely teaching example.
namespace ExampleMod.Common.Configs.ModConfigShowcases;

/// <summary>
/// This config is just a showcase of various attributes and their effects in the UI window.
/// </summary>
public class ModConfigShowcaseMisc : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("Colors")]

    // You can put multiple attributes in the same [] if you like.
    // ColorHueSliderAttribute displays Hue Saturation Lightness. Passing in false means only Hue is shown.
    [DefaultValue(typeof(Color), "255, 0, 0, 255"), ColorHSLSlider(false), ColorNoAlpha]
    public Color hsl;

    // Enum can be presented as a chooser (default), slider, dropdown, or cycle button:
    [Header("EnumOptions")]
    public SampleEnum EnumDefault;

    [Slider, DrawTicks]
    public SampleEnum EnumSlider;

    [Dropdown]
    public SampleEnum EnumDropdown;

    [Cycle]
    public SampleEnum EnumCycle;
}
