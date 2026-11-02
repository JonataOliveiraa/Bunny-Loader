using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases;

[BackgroundColor(99, 180, 209)]
public class ModConfigShowcaseRanges : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public float NormalFloat;

    [Range(2f, 3f)]
    [Increment(.25f)]
    [DrawTicks]
    [DefaultValue(2f)]
    public float IncrementalRangedFloat;

    [Range(0f, 5f)]
    [Increment(.11f)]
    public float IncrementByPoint11;

    [Range(2f, 5f)]
    [DefaultValue(2f)]
    public float RangedFloat;

    public int NormalInt;

    [Increment(5)]
    [Range(60, 250)]
    [DefaultValue(100)]
    [Slider]
    public int RangedInteger;

    [Range(10, 20)]
    [Increment(2)]
    [DrawTicks]
    [DefaultListValue(16)]
    [Slider]
    public List<int> ListOfInts = new List<int>();

    [Range(-20f, 20f)]
    [Increment(5f)]
    [DrawTicks]
    public Vector2 RangedWithIncrementVector2;
}
