using System;

namespace Terraria.Localization;

internal static class Formatting
{
    public static string Apply(string text, object?[] args)
    {
        if (args.Length == 0) return text;
        try
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, @"{\^(\d+):([^{};]*);([^{}]*)}", match =>
            {
                int index = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (index >= args.Length) return match.Value;
                return System.Convert.ToDouble(args[index], System.Globalization.CultureInfo.InvariantCulture) == 1 ? match.Groups[2].Value : match.Groups[3].Value;
            });
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }
}
