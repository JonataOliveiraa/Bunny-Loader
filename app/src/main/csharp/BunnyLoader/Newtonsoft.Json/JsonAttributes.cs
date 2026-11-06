using System;

namespace Newtonsoft.Json
{
    public enum MemberSerialization
    {
        OptOut,
        OptIn,
        Fields
    }

    public abstract class JsonConverter
    {
    }

    public class JsonException : Exception
    {
        public JsonException()
        {
        }

        public JsonException(string message) : base(message)
        {
        }

        public JsonException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class JsonPropertyAttribute : Attribute
    {
        public string? PropertyName { get; set; }

        public int Order { get; set; }

        public JsonPropertyAttribute()
        {
        }

        public JsonPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonRequiredAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonExtensionDataAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class JsonConstructorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class JsonObjectAttribute : Attribute
    {
        public MemberSerialization MemberSerialization { get; set; }

        public JsonObjectAttribute()
        {
        }

        public JsonObjectAttribute(MemberSerialization memberSerialization)
        {
            MemberSerialization = memberSerialization;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Parameter)]
    public sealed class JsonConverterAttribute : Attribute
    {
        public Type ConverterType { get; }

        public object[]? ConverterParameters { get; }

        public JsonConverterAttribute(Type converterType)
        {
            ConverterType = converterType;
        }

        public JsonConverterAttribute(Type converterType, params object[] converterParameters)
        {
            ConverterType = converterType;
            ConverterParameters = converterParameters;
        }
    }
}

namespace Newtonsoft.Json.Converters
{
    public class StringEnumConverter : JsonConverter
    {
    }
}
