using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.Localization;

public readonly partial struct LocalizedText
{
    static readonly Dictionary<(string Key, string Value), GameHandle> Kept = new();
    public string Format(params object?[] args) => Formatting.Apply(Value ?? "", args);
    public LocalizedText WithFormatArgs(params object?[] args) => Cached(Key ?? "", Format(args));
    public static LocalizedText Literal(string value) => Cached("", value);
    static LocalizedText Cached(string key, string value)
    {
        lock (Kept)
        {
            if (!Kept.TryGetValue((key, value), out var pin))
            {
                LocalizedText text = New(key, value);
                Kept[(key, value)] = pin = new GameHandle(text.Pointer);
            }
            return new LocalizedText(pin.Target);
        }
    }
}
