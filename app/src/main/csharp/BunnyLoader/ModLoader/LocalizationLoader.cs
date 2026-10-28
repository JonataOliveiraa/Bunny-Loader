using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using BunnyLoader;

namespace Terraria.ModLoader;

// As traducoes do mod: content/Localization/<cultura>.json, no mesmo formato
// dos mods JS ({ "ItemName": { "ExampleItem": "Exemplo de Item" }, ... }).
// Lidas uma vez por mod, na carga.
internal static class LocalizationLoader
{
    static readonly Dictionary<Mod, Dictionary<string, Dictionary<string, Dictionary<string, string>>>> Cache = new();

    // (cultura, texto) da chave na secao, em cada idioma que a tem.
    public static List<(string Culture, string Text)> Texts(Mod mod, string section, string key)
    {
        var found = new List<(string, string)>();
        foreach ((string culture, var sections) in Files(mod))
        {
            if (sections.TryGetValue(section, out var entries) && entries.TryGetValue(key, out string? text))
                found.Add((culture, text));
        }
        return found;
    }

    // Para o nativo: o texto sozinho (qualquer cultura) ou "cultura\x1Ftexto"
    // separados por \x1E.
    public static string Pack(IReadOnlyList<(string Culture, string Text)> texts)
    {
        if (texts.Count == 1 && texts[0].Culture.Length == 0) return texts[0].Text;
        var packed = new StringBuilder();
        foreach ((string culture, string text) in texts)
        {
            if (packed.Length > 0) packed.Append('\x1E');
            packed.Append(culture).Append('\x1F').Append(text);
        }
        return packed.ToString();
    }

    static Dictionary<string, Dictionary<string, Dictionary<string, string>>> Files(Mod mod)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(mod, out var loaded)) return loaded;
            loaded = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            string folder = Path.Combine(mod.Folder, "Localization");
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    try
                    {
                        loaded[Path.GetFileNameWithoutExtension(file)] = Read(file);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"mod {mod.Uid}: {Path.GetFileName(file)}: {e.Message}");
                    }
                }
            }
            Cache[mod] = loaded;
            return loaded;
        }
    }

    static Dictionary<string, Dictionary<string, string>> Read(string file)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file), options);
        var sections = new Dictionary<string, Dictionary<string, string>>();
        foreach (JsonProperty section in document.RootElement.EnumerateObject())
        {
            if (section.Value.ValueKind != JsonValueKind.Object) continue;
            var entries = new Dictionary<string, string>();
            foreach (JsonProperty entry in section.Value.EnumerateObject())
            {
                if (entry.Value.ValueKind == JsonValueKind.String) entries[entry.Name] = entry.Value.GetString()!;
            }
            sections[section.Name] = entries;
        }
        return sections;
    }
}
