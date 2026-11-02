using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;

namespace Terraria.ModLoader.Config;

internal enum ConfigOptionKind
{
    Toggle,
    Range,
    Choice,
    Color,
    Data
}

internal sealed class ConfigOption
{
    readonly FieldInfo? field;

    readonly PropertyInfo? property;

    internal ConfigOption(MemberInfo member)
    {
        field = member as FieldInfo;
        property = member as PropertyInfo;
        Member = member;
        Type = field?.FieldType ?? property!.PropertyType;
        ReloadRequired = member.GetCustomAttribute<ReloadRequiredAttribute>() != null;
        Header = member.GetCustomAttribute<HeaderAttribute>();
        LabelKey = member.GetCustomAttribute<LabelKeyAttribute>();
        LabelArgs = member.GetCustomAttribute<LabelArgsAttribute>();
        Slider = member.GetCustomAttribute<SliderAttribute>() != null || Type.GetCustomAttribute<SliderAttribute>() != null;
        ColorNoAlpha = member.GetCustomAttribute<ColorNoAlphaAttribute>() != null;
        var hsl = member.GetCustomAttribute<ColorHSLSliderAttribute>();
        ShowSaturationAndLightness = hsl?.ShowSaturationAndLightness ?? true;
        Kind = KindOf(member);
        Dropdown = member.GetCustomAttribute<DropdownAttribute>() != null || Type.GetCustomAttribute<DropdownAttribute>() != null;
        Cycle = member.GetCustomAttribute<CycleAttribute>() != null || Type.GetCustomAttribute<CycleAttribute>() != null;
    }

    internal bool Dropdown { get; }

    internal bool Cycle { get; }

    internal MemberInfo Member { get; }

    internal string Name => Member.Name;

    internal Type Type { get; }

    internal ConfigOptionKind Kind { get; }

    internal bool ReloadRequired { get; }

    internal HeaderAttribute? Header { get; }

    internal LabelKeyAttribute? LabelKey { get; }

    internal LabelArgsAttribute? LabelArgs { get; }

    internal bool Slider { get; }

    internal bool ColorNoAlpha { get; }

    internal bool ShowSaturationAndLightness { get; }

    internal double Min { get; private set; }

    internal double Max { get; private set; }

    internal double Step { get; private set; }

    internal bool Integer { get; private set; }

    internal object[] Choices { get; private set; } = Array.Empty<object>();

    internal bool HasDefault { get; private set; }

    internal object? Default { get; private set; }

    internal object? Get(object owner) => field != null ? field.GetValue(owner) : property!.GetValue(owner);

    internal void Set(object owner, object? value)
    {
        if (field != null)
            field.SetValue(owner, value);
        else
            property!.SetValue(owner, value);
    }

    static bool IsInteger(Type type) =>
        type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) || type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte);

    ConfigOptionKind KindOf(MemberInfo member)
    {
        ReadDefault(member);
        if (Type == typeof(bool))
            return ConfigOptionKind.Toggle;
        if (Type == typeof(Color))
            return ConfigOptionKind.Color;
        if (Type.IsEnum)
        {
            Choices = Enum.GetValues(Type).Cast<object>().ToArray();
            return ConfigOptionKind.Choice;
        }

        if (Type == typeof(string) && member.GetCustomAttribute<OptionStringsAttribute>() is { } strings && strings.OptionLabels.Length > 1)
        {
            Choices = strings.OptionLabels.Cast<object>().ToArray();
            return ConfigOptionKind.Choice;
        }

        if (IsInteger(Type) || Type == typeof(float) || Type == typeof(double))
        {
            Integer = IsInteger(Type);
            var range = member.GetCustomAttribute<RangeAttribute>();
            var increment = member.GetCustomAttribute<IncrementAttribute>();
            Min = range != null ? Convert.ToDouble(range.Min, CultureInfo.InvariantCulture) : 0;
            Max = range != null ? Convert.ToDouble(range.Max, CultureInfo.InvariantCulture) : Integer ? 100 : 1;
            Step = increment != null ? Convert.ToDouble(increment.Increment, CultureInfo.InvariantCulture) : Integer ? 1 : 0.01;
            if (Type == typeof(byte) && range == null)
                Max = 255;
            if (!(Max > Min) || !(Step > 0))
                return ConfigOptionKind.Data;
            return ConfigOptionKind.Range;
        }

        return ConfigOptionKind.Data;
    }

    void ReadDefault(MemberInfo member)
    {
        foreach (CustomAttributeData data in member.GetCustomAttributesData())
        {
            if (data.AttributeType != typeof(DefaultValueAttribute))
                continue;
            HasDefault = true;
            var args = data.ConstructorArguments;
            if (args.Count == 2 && args[0].Value is Type target && args[1].Value is string text)
                Default = Parse(target, text);
            else if (args.Count == 1)
                Default = args[0].Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> list ? list.Select(a => a.Value).ToArray() : args[0].Value;
            return;
        }
    }

    internal object? Coerce(object? value)
    {
        try
        {
            if (value == null)
                return null;
            if (Kind == ConfigOptionKind.Color)
                return value is Color color ? color : value is string text ? ParseColor(text) : null;
            if (Type.IsEnum)
                return value is string name ? Enum.Parse(Type, name, true) : Enum.ToObject(Type, value);
            if (Kind == ConfigOptionKind.Range)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return Convert.ChangeType(Snap(number), Type, CultureInfo.InvariantCulture);
            }

            if (Kind == ConfigOptionKind.Choice)
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                return Choices.Contains(text) ? text : null;
            }

            return Type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, Type, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    internal double Snap(double value)
    {
        double steps = Math.Round((value - Min) / Step);
        double snapped = Math.Round(Min + steps * Step, 6);
        snapped = Math.Clamp(snapped, Min, Max);
        return Integer ? Math.Round(snapped) : snapped;
    }

    static object? Parse(Type target, string text) => target == typeof(Color) ? ParseColor(text) : TypeDescriptor.GetConverter(target).ConvertFromInvariantString(text);

    internal static Color? ParseColor(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith("#") && (trimmed.Length == 7 || trimmed.Length == 9) &&
            uint.TryParse(trimmed[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
        {
            if (trimmed.Length == 7)
                return new Color((int)(hex >> 16) & 255, (int)(hex >> 8) & 255, (int)hex & 255, 255);
            return new Color((int)(hex >> 24) & 255, (int)(hex >> 16) & 255, (int)(hex >> 8) & 255, (int)hex & 255);
        }

        string[] parts = trimmed.Split(',');
        if (parts.Length is < 3 or > 4)
            return null;
        int[] values = new int[4] { 0, 0, 0, 255 };
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                return null;
        return new Color(values[0], values[1], values[2], values[3]);
    }

    internal static string FormatColor(Color color) => $"{color.R}, {color.G}, {color.B}, {color.A}";

    internal string Label(ModConfig config)
    {
        string key = LabelKey != null && !LabelKey.malformed ? LabelKey.key : $"Mods.{config.Mod.Name}.Configs.{config.Name}.{Name}.Label";
        string text = Language.GetOrRegister(key, () => Regex.Replace(Name, "([a-z0-9])([A-Z])", "$1 $2")).Value ?? Name;
        return LabelArgs != null && LabelArgs.args.Length > 0 ? Formatting.Apply(text, LabelArgs.args) : text;
    }

    internal string? HeaderText(ModConfig config)
    {
        if (Header == null || Header.malformed)
            return null;
        if (Header.key != null)
            return Language.GetTextValue(Header.key);
        string id = Header.identifier!;
        return Language.GetOrRegister($"Mods.{config.Mod.Name}.Configs.{config.Name}.Headers.{id}", () => Regex.Replace(id, "([a-z0-9])([A-Z])", "$1 $2")).Value;
    }

    internal string ChoiceLabel(ModConfig config, int index)
    {
        object choice = Choices[index];
        if (!Type.IsEnum)
            return (string)choice;
        string name = choice.ToString() ?? "";
        FieldInfo? value = Type.GetField(name);
        var key = value?.GetCustomAttribute<LabelKeyAttribute>();
        string full = key != null && !key.malformed ? key.key : $"Mods.{config.Mod.Name}.Configs.{Type.Name}.{name}.Label";
        return Language.GetOrRegister(full, () => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2")).Value ?? name;
    }

    internal static bool Shown(MemberInfo member)
    {
        if (member.GetCustomAttribute<JsonIgnoreAttribute>() != null && member.GetCustomAttribute<ShowDespiteJsonIgnoreAttribute>() == null)
            return false;
        if (member is PropertyInfo property)
            return property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && property.GetMethod!.IsPublic && property.SetMethod!.IsPublic;
        return member is FieldInfo { IsInitOnly: false, IsLiteral: false };
    }
}
