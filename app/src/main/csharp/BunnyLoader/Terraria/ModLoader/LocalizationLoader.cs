using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BunnyLoader;
using Terraria.Localization;

namespace Terraria.ModLoader;

internal static unsafe class LocalizationLoader
{
    static readonly Regex CultureName = new(@"^[a-z]{2,3}(-[A-Za-z]{2,4})?$", RegexOptions.CultureInvariant);

    static readonly Dictionary<Mod, Dictionary<string, List<(string Culture, string Text)>>> ByMod = new();

    public static void Load(Mod mod)
    {
        var all = new Dictionary<string, List<(string Culture, string Text)>>(StringComparer.Ordinal);
        foreach (string file in ModFiles(mod, "*.hjson"))
        {
            string relative = Path.GetRelativePath(mod.Folder, file).Replace('\\', '/');
            if (!CultureAndPrefix(relative, out string culture, out string prefix)) continue;
            try
            {
                foreach ((string key, string text) in HjsonReader.Flatten(File.ReadAllText(file), prefix)) Add(all, key, culture, text, replace: true);
            }
            catch (Exception e)
            {
                Log.Error($"mod {mod.Uid}: {relative}: {e.Message}");
            }
        }
        LoadLegacyJson(mod, all);
        lock (ByMod) ByMod[mod] = all;
        foreach ((string key, List<(string Culture, string Text)> texts) in all) Native(key, Pack(texts), replace: true);
    }

    public static List<(string Culture, string Text)> Texts(Mod mod, string key)
    {
        lock (ByMod)
        {
            return ByMod.TryGetValue(mod, out var all) && all.TryGetValue(key, out var texts)
                ? new List<(string Culture, string Text)>(texts)
                : new List<(string Culture, string Text)>();
        }
    }

    public static LocalizedText GetOrRegister(string key, Func<string>? makeDefaultValue)
    {
        IntPtr found = Native(key, null, replace: false);
        if (found != IntPtr.Zero) return new LocalizedText(found);
        IntPtr created = Native(key, makeDefaultValue?.Invoke() ?? key, replace: false);
        if (created == IntPtr.Zero) Log.Error($"localization: could not register '{key}'");
        return new LocalizedText(created);
    }

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

    static IntPtr Native(string key, string? packed, bool replace)
    {
        byte* k = Host.Utf8(key);
        byte* p = packed == null ? null : Host.Utf8(packed);
        try { return Host.Native()->LanguageText(k, p, replace ? 1 : 0); }
        finally
        {
            Host.FreeUtf8(k);
            if (p != null) Host.FreeUtf8(p);
        }
    }

    static void Add(Dictionary<string, List<(string Culture, string Text)>> all, string key, string culture, string text, bool replace)
    {
        if (!all.TryGetValue(key, out var texts)) all[key] = texts = new List<(string Culture, string Text)>();
        int index = texts.FindIndex(t => t.Culture == culture);
        if (index < 0) texts.Add((culture, text));
        else if (replace) texts[index] = (culture, text);
    }

    static bool CultureAndPrefix(string relative, out string culture, out string prefix)
    {
        culture = "";
        prefix = "";
        string[] segments = Path.ChangeExtension(relative, null)!.Split('/');
        for (int s = 0; s < segments.Length; s++)
        {
            string[] parts = segments[s].Split('_');
            for (int p = 0; p < parts.Length; p++)
            {
                if (!CultureName.IsMatch(parts[p])) continue;
                culture = parts[p];
                prefix = string.Join("_", parts.Skip(p + 1));
                if (prefix.Length == 0 && s < segments.Length - 1) prefix = segments[^1];
                return true;
            }
        }
        return false;
    }

    static IEnumerable<string> ModFiles(Mod mod, string pattern)
    {
        if (!Directory.Exists(mod.Folder)) return Array.Empty<string>();
        return Directory.EnumerateFiles(mod.Folder, pattern, SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(mod.Folder, f).Replace('\\', '/').Split('/')[..^1]
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase) || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    static void LoadLegacyJson(Mod mod, Dictionary<string, List<(string Culture, string Text)>> all)
    {
        string folder = Path.Combine(mod.Folder, "Localization");
        if (!Directory.Exists(folder)) return;
        var sections = new Dictionary<string, string> { ["ItemName"] = "DisplayName", ["ItemTooltip"] = "Tooltip" };
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (string file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            string culture = Path.GetFileNameWithoutExtension(file);
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file), options);
                foreach (JsonProperty section in document.RootElement.EnumerateObject())
                {
                    if (!sections.TryGetValue(section.Name, out string? suffix) || section.Value.ValueKind != JsonValueKind.Object) continue;
                    foreach (JsonProperty entry in section.Value.EnumerateObject())
                    {
                        if (entry.Value.ValueKind != JsonValueKind.String) continue;
                        Add(all, $"Mods.{mod.Name}.Items.{entry.Name}.{suffix}", culture, entry.Value.GetString()!, replace: false);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"mod {mod.Uid}: {Path.GetFileName(file)}: {e.Message}");
            }
        }
    }
}
