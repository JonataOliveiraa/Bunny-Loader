using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.Localization;

public readonly partial struct LocalizedText
{
    static readonly List<GameHandle> Kept = new();

    public string Format(params object?[] args) => Formatting.Apply(Value ?? "", args);

    public LocalizedText WithFormatArgs(params object?[] args)
    {
        LocalizedText text = New(Key, Format(args));
        lock (Kept) Kept.Add(new GameHandle(text.Pointer));
        return text;
    }
}
