using System;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Config;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public class BackgroundColorAttribute : Attribute
{
    public Color Color { get; }

    public BackgroundColorAttribute(int r, int g, int b, int a = 255)
    {
        Color = new Color(r, g, b, a);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public class SliderColorAttribute : Attribute
{
    public Color Color { get; }

    public SliderColorAttribute(int r, int g, int b, int a = 255)
    {
        Color = new Color(r, g, b, a);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ReloadRequiredAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public abstract class ConfigKeyAttribute : Attribute
{
    internal readonly string key;

    internal readonly bool malformed;

    public ConfigKeyAttribute(string key)
    {
        if (!key.StartsWith("$"))
        {
            malformed = true;
            this.key = key;
        }
        else
        {
            this.key = key.Substring(1);
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public abstract class ConfigArgsAttribute : Attribute
{
    internal readonly object[] args;

    public ConfigArgsAttribute(params object[] args)
    {
        this.args = args;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class LabelKeyAttribute : ConfigKeyAttribute
{
    public LabelKeyAttribute(string key) : base(key)
    {
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class LabelArgsAttribute : ConfigArgsAttribute
{
    public LabelArgsAttribute(params object[] args) : base(args)
    {
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class TooltipKeyAttribute : ConfigKeyAttribute
{
    public TooltipKeyAttribute(string key) : base(key)
    {
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class TooltipArgsAttribute : ConfigArgsAttribute
{
    public TooltipArgsAttribute(params object[] args) : base(args)
    {
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ShowDespiteJsonIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class HeaderAttribute : Attribute
{
    internal string? key;

    internal string? identifier;

    internal readonly bool malformed;

    public bool IsIdentifier => identifier != null;

    public string Header => Localization.Language.GetTextValue(key ?? "");

    public HeaderAttribute(string identifierOrKey)
    {
        if (string.IsNullOrWhiteSpace(identifierOrKey) || identifierOrKey.Contains(' '))
        {
            malformed = true;
            return;
        }

        if (!identifierOrKey.StartsWith("$"))
            identifier = identifierOrKey;
        else
            key = identifierOrKey.Substring(1);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class OptionStringsAttribute : Attribute
{
    public string[] OptionLabels { get; set; }

    public OptionStringsAttribute(string[] optionLabels)
    {
        OptionLabels = optionLabels;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class IncrementAttribute : Attribute
{
    public object Increment { get; }

    public IncrementAttribute(int increment) => Increment = increment;

    public IncrementAttribute(float increment) => Increment = increment;

    public IncrementAttribute(uint increment) => Increment = increment;

    public IncrementAttribute(long increment) => Increment = increment;

    public IncrementAttribute(ulong increment) => Increment = increment;

    public IncrementAttribute(byte increment) => Increment = increment;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class RangeAttribute : Attribute
{
    public object Min { get; }

    public object Max { get; }

    public RangeAttribute(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public RangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public RangeAttribute(uint min, uint max)
    {
        Min = min;
        Max = max;
    }

    public RangeAttribute(long min, long max)
    {
        Min = min;
        Max = max;
    }

    public RangeAttribute(ulong min, ulong max)
    {
        Min = min;
        Max = max;
    }

    public RangeAttribute(byte min, byte max)
    {
        Min = min;
        Max = max;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Enum)]
public class SliderAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DrawTicksAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ColorNoAlphaAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ColorHSLSliderAttribute : Attribute
{
    public bool ShowSaturationAndLightness { get; }

    public ColorHSLSliderAttribute(bool showSaturationAndLightness = true)
    {
        ShowSaturationAndLightness = showSaturationAndLightness;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public class SeparatePageAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public class NullAllowedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
public class ExpandAttribute : Attribute
{
    public bool Expand { get; }

    public bool? ExpandListElements { get; }

    public ExpandAttribute(bool expand = true)
    {
        Expand = expand;
        ExpandListElements = null;
    }

    public ExpandAttribute(bool expand = true, bool expandListElements = true)
    {
        Expand = expand;
        ExpandListElements = expandListElements;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Enum)]
public class CustomModConfigItemAttribute : Attribute
{
    public Type Type { get; }

    public CustomModConfigItemAttribute(Type type)
    {
        Type = type;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class JsonDefaultValueAttribute : Attribute
{
    public string Json { get; }

    public JsonDefaultValueAttribute(string json)
    {
        Json = json;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Enum)]
public class DropdownAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Enum)]
public class CycleAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultListValueAttribute : Attribute
{
    public object? Value { get; }

    public DefaultListValueAttribute(Type type, string value) => Value = System.ComponentModel.TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value);

    public DefaultListValueAttribute(char value) => Value = value;

    public DefaultListValueAttribute(byte value) => Value = value;

    public DefaultListValueAttribute(short value) => Value = value;

    public DefaultListValueAttribute(int value) => Value = value;

    public DefaultListValueAttribute(long value) => Value = value;

    public DefaultListValueAttribute(float value) => Value = value;

    public DefaultListValueAttribute(double value) => Value = value;

    public DefaultListValueAttribute(bool value) => Value = value;

    public DefaultListValueAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultDictionaryKeyValueAttribute : Attribute
{
    public virtual object? Value { get; }

    public DefaultDictionaryKeyValueAttribute(Type type, string value) => Value = System.ComponentModel.TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value);

    public DefaultDictionaryKeyValueAttribute(char value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(byte value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(short value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(int value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(long value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(float value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(double value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(bool value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(string value) => Value = value;

    public DefaultDictionaryKeyValueAttribute(object value) => Value = value;
}
