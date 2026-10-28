using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Terraria.ModLoader.IO;

internal static class TagJson
{
    const int MaxDepth = 128;

    public static string Write(TagCompound tag)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCompound(writer, tag, 0);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static TagCompound Read(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth + 2 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("TagCompound: the saved data is not an object");
        return ReadCompound(document.RootElement);
    }

    static void WriteCompound(Utf8JsonWriter writer, TagCompound tag, int depth)
    {
        if (depth > MaxDepth) throw new InvalidOperationException("TagCompound: too deep");
        writer.WriteStartObject();
        foreach ((string key, object value) in tag)
        {
            writer.WritePropertyName(key);
            WriteValue(writer, value, depth + 1);
        }
        writer.WriteEndObject();
    }

    static void WriteValue(Utf8JsonWriter writer, object value, int depth)
    {
        switch (value)
        {
            case byte b: writer.WriteNumberValue(b); break;
            case short s: writer.WriteNumberValue(s); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case float f: WriteReal(writer, f); writer.WriteNumberValue(f); break;
            case double d: WriteReal(writer, d); writer.WriteNumberValue(d); break;
            case string s: writer.WriteStringValue(s); break;
            case TagCompound tag: WriteCompound(writer, tag, depth); break;
            case byte[] bytes:
                writer.WriteStartArray();
                foreach (byte b in bytes) writer.WriteNumberValue(b);
                writer.WriteEndArray();
                break;
            case int[] ints:
                writer.WriteStartArray();
                foreach (int i in ints) writer.WriteNumberValue(i);
                writer.WriteEndArray();
                break;
            case List<object> list:
                if (depth > MaxDepth) throw new InvalidOperationException("TagCompound: too deep");
                writer.WriteStartArray();
                foreach (object element in list) WriteValue(writer, element, depth + 1);
                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"TagCompound: {value.GetType().Name} cannot be saved");
        }
    }

    static void WriteReal(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("TagCompound: NaN and infinity cannot be saved");
    }

    static TagCompound ReadCompound(JsonElement element)
    {
        var tag = new TagCompound();
        foreach (JsonProperty property in element.EnumerateObject())
        {
            object? value = ReadValue(property.Value);
            if (value != null) tag.Set(property.Name, value, true);
        }
        return tag;
    }

    static object? ReadValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return ReadCompound(element);
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    object? value = ReadValue(item);
                    if (value != null) list.Add(value);
                }
                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int i)) return i;
                if (element.TryGetInt64(out long l)) return l;
                return element.GetDouble();
            default:
                return null;
        }
    }
}
