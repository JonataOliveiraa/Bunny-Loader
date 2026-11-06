using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Converters;

namespace Terraria.ModLoader.Config;

internal static class ConfigJson
{
    internal static readonly JsonSerializerOptions Options = Build(null);

    internal static JsonSerializerOptions Build(Type? plain)
    {
        var options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Honour } },
        };
        options.Converters.Add(new ColorConverter());
        options.Converters.Add(new Vector2Converter());
        options.Converters.Add(new PointConverter());
        options.Converters.Add(new KeyedFactory(plain));
        return options;
    }

    static void Honour(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
            return;
        for (int i = info.Properties.Count - 1; i >= 0; i--)
        {
            JsonPropertyInfo property = info.Properties[i];
            ICustomAttributeProvider? provider = property.AttributeProvider;
            if (provider == null)
                continue;
            bool Has<T>() => provider.GetCustomAttributes(typeof(T), true).Length > 0;
            if (Has<Newtonsoft.Json.JsonIgnoreAttribute>() || property.Set == null && provider is PropertyInfo)
            {
                info.Properties.RemoveAt(i);
                continue;
            }

            if (provider.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), true).FirstOrDefault() is Newtonsoft.Json.JsonPropertyAttribute { PropertyName: { } name })
                property.Name = name;
            if (provider.GetCustomAttributes(typeof(Newtonsoft.Json.JsonConverterAttribute), true).FirstOrDefault() is Newtonsoft.Json.JsonConverterAttribute { ConverterType: var converter } && converter == typeof(StringEnumConverter) && property.PropertyType.IsEnum)
                property.CustomConverter = new JsonStringEnumConverter();
        }

        foreach (MethodInfo method in info.Type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<OnDeserializedAttribute>() == null || method.GetParameters().Length != 1)
                continue;
            info.OnDeserialized = target => method.Invoke(target, new object[] { default(StreamingContext) });
        }
    }

    sealed class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ConfigOption.ParseColor(reader.GetString() ?? "") ?? default;

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
            writer.WriteStringValue(ConfigOption.FormatColor(value));
    }

    sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ConfigOption.ParseVector2(reader.GetString() ?? "");

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options) =>
            writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{value.X}, {value.Y}"));
    }

    sealed class PointConverter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ConfigOption.ParsePoint(reader.GetString() ?? "");

        public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options) =>
            writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{value.X}, {value.Y}"));
    }

    sealed class KeyedFactory : JsonConverterFactory
    {
        readonly Type? plain;

        internal KeyedFactory(Type? plain)
        {
            this.plain = plain;
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert != plain && !typeToConvert.IsAbstract && typeToConvert.GetCustomAttribute<TypeConverterAttribute>() != null && typeToConvert.GetMethod("FromString", new[] { typeof(string) }) != null;

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter?)Activator.CreateInstance(typeof(KeyedConverter<>).MakeGenericType(typeToConvert));
    }

    sealed class KeyedConverter<T> : JsonConverter<T>
    {
        readonly JsonSerializerOptions inner = Build(typeof(T));

        readonly MethodInfo parse = typeof(T).GetMethod("FromString", new[] { typeof(string) })!;

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? (T?)parse.Invoke(null, new object?[] { reader.GetString() }) : JsonSerializer.Deserialize<T>(ref reader, inner);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, inner);

        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (T)parse.Invoke(null, new object?[] { reader.GetString() })!;

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WritePropertyName(value?.ToString() ?? "");
    }
}
