using System;
using System.Collections.Generic;
using System.Text.Json;
using BunnyLoader.Interop;

namespace Terraria.IO;

public readonly unsafe partial struct GameConfiguration
{
    static readonly GameClass TokenClass = new("Newtonsoft.Json.Linq", "JToken");

    static MethodRef toString;

    static VirtualCache? toStringCache;

    static readonly int[] ToStringLayout =
    {
        -1
    };

    public T Get<T>(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (IsNull || _root.IsNull)
            throw new KeyNotFoundException($"No generation configuration entry '{entry}'");
        ref readonly MethodRef method = ref GameInterop.Method(ref toString, TokenClass, "string ToString()", ToStringLayout);
        var target = GameInterop.Virtual(ref toStringCache, in method, _root.Pointer);
        CallFrame frame;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out frame);
        frame.X[0] = (long)_root.Pointer;
        frame.X[1] = (long)target.Info;
        frame.Ret = RetKind.Int;
        GameInterop.CallFast(target.Fn, &frame);
        string json = GameString.Read((IntPtr)frame.Out[0]) ?? "{}";
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(entry, out JsonElement value))
            throw new KeyNotFoundException($"No generation configuration entry '{entry}'");
        return value.Deserialize<T>()!;
    }
}
