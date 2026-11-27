using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Design;

public class ColorConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);

        culture ??= CultureInfo.CurrentCulture;
        string[] parts = text.Split(culture.TextInfo.ListSeparator.ToCharArray());
        if (parts.Length != 4) throw new ArgumentException($"Color: expected 4 values separated by '{culture.TextInfo.ListSeparator}', got \"{text}\"", nameof(value));

        return new Color(
            int.Parse(parts[0], NumberStyles.Integer, culture),
            int.Parse(parts[1], NumberStyles.Integer, culture),
            int.Parse(parts[2], NumberStyles.Integer, culture),
            int.Parse(parts[3], NumberStyles.Integer, culture));
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Color color)
        {
            culture ??= CultureInfo.CurrentCulture;
            string separator = culture.TextInfo.ListSeparator + " ";
            return string.Join(separator, color.R.ToString(culture), color.G.ToString(culture), color.B.ToString(culture), color.A.ToString(culture));
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary propertyValues)
    {
        ArgumentNullException.ThrowIfNull(propertyValues);
        return new Color(
            Convert.ToInt32(propertyValues["R"], CultureInfo.InvariantCulture),
            Convert.ToInt32(propertyValues["G"], CultureInfo.InvariantCulture),
            Convert.ToInt32(propertyValues["B"], CultureInfo.InvariantCulture),
            Convert.ToInt32(propertyValues["A"], CultureInfo.InvariantCulture));
    }
}
