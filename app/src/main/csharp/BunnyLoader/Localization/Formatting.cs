using System;

namespace Terraria.Localization;

internal static class Formatting
{
    public static string Apply(string text, object?[] args)
    {
        if (args.Length == 0) return text;
        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }
}
