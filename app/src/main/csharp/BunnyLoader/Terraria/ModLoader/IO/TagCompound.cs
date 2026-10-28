using System;
using System.Collections;
using System.Collections.Generic;

namespace Terraria.ModLoader.IO;

public class TagCompound : IEnumerable<KeyValuePair<string, object>>, ICloneable
{
    readonly Dictionary<string, object> values = new(StringComparer.Ordinal);

    public int Count => values.Count;

    public object this[string key]
    {
        get => Get<object>(key);
        set => Set(key, value, true);
    }

    public T Get<T>(string key)
    {
        values.TryGetValue(key, out object? value);
        try
        {
            return TagConvert.To<T>(value);
        }
        catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidCastException($"TagCompound: '{key}' is {value?.GetType().Name ?? "missing"}, not {typeof(T).Name}", e);
        }
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (values.TryGetValue(key, out object? raw))
        {
            try
            {
                value = TagConvert.To<T>(raw);
                return true;
            }
            catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
            {
            }
        }
        value = default!;
        return false;
    }

    public void Set(string key, object? value, bool replace = false)
    {
        if (value == null)
        {
            Remove(key);
            return;
        }
        object stored = TagConvert.Normalize(value);
        if (!replace && values.ContainsKey(key)) throw new ArgumentException($"TagCompound: '{key}' is already set (use the indexer to replace it)", nameof(key));
        values[key] = stored;
    }

    public void Add(string key, object? value) => Set(key, value);

    public void Add(KeyValuePair<string, object> entry) => Set(entry.Key, entry.Value);

    public bool ContainsKey(string key) => values.ContainsKey(key);

    public bool Remove(string key) => values.Remove(key);

    public void Clear() => values.Clear();

    public bool GetBool(string key) => Get<bool>(key);

    public byte GetByte(string key) => Get<byte>(key);

    public short GetShort(string key) => Get<short>(key);

    public int GetInt(string key) => Get<int>(key);

    public long GetLong(string key) => Get<long>(key);

    public float GetFloat(string key) => Get<float>(key);

    public double GetDouble(string key) => Get<double>(key);

    public string GetString(string key) => Get<string>(key) ?? "";

    public byte[] GetByteArray(string key) => Get<byte[]>(key);

    public int[] GetIntArray(string key) => Get<int[]>(key);

    public TagCompound GetCompound(string key) => Get<TagCompound>(key) ?? new TagCompound();

    public IList<T> GetList<T>(string key) => Get<List<T>>(key);

    public object Clone()
    {
        var copy = new TagCompound();
        foreach ((string key, object value) in values) copy.values[key] = TagConvert.Copy(value);
        return copy;
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => TagJson.Write(this);
}
