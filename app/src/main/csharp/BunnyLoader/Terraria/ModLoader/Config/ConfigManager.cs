using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader.Config;

public static class ConfigManager
{
    internal static readonly Dictionary<Mod, List<ModConfig>> Configs = new();

    static readonly Dictionary<Mod, List<ModConfig>> loadTimeConfigs = new();

    static readonly Dictionary<Type, ConfigOption[]> options = new();

    static string? savePath;

    public static string ModConfigPath => Path.Combine(SavePath(), "ModConfigs");

    internal static string? FormatTextAttribute(LocalizedText text, object[]? args)
    {
        string? value = text.Value;
        if (value == null)
            return null;
        if (args != null && args.Length > 0)
        {
            object[] resolved = (object[])args.Clone();
            for (int i = 0; i < resolved.Length; i++)
                if (resolved[i] is string s && s.StartsWith('$'))
                    resolved[i] = Language.GetTextValue(FindKeyInScope(s.Substring(1), text.Key ?? ""));
            value = Formatting.Apply(value, resolved);
        }

        return ResolveItemTags(value);
    }

    static string FindKeyInScope(string key, string scope)
    {
        if (LanguageManager.Instance.Exists(key))
            return key;
        string[] splitKey = scope.Split('.');
        for (int j = splitKey.Length - 1; j >= 0; j--)
        {
            string combinedKey = string.Join(".", splitKey.Take(j + 1)) + "." + key;
            if (LanguageManager.Instance.Exists(combinedKey))
                return combinedKey;
        }

        return key;
    }

    internal static string ResolveItemTags(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\[i(/[^:\]]*)?:([^\]]+)\]", match =>
    {
        string name = match.Groups[2].Value;
        if (int.TryParse(name, out _))
            return match.Value;
        int type = ModContent.TryFind(name, out ModItem item) ? item.Type : ItemID.Search.TryGetId(name, out int id) ? id : 0;
        return type > 0 ? $"[i{match.Groups[1].Value}:{type}]" : match.Value;
    });

    static string SavePath()
    {
        if (savePath != null)
            return savePath;
        try
        {
            return Main.SavePath ?? "";
        }
        catch
        {
            return "";
        }
    }

    static void FindSavePath(Mod mod)
    {
        if (savePath != null || string.IsNullOrEmpty(mod.Folder))
            return;
        for (DirectoryInfo? dir = new DirectoryInfo(mod.Folder); dir != null; dir = dir.Parent)
            if (dir.Name == "bunny_packs" && dir.Parent != null)
            {
                savePath = dir.Parent.FullName;
                return;
            }
    }

    internal static IEnumerable<ModConfig> All => Configs.Values.SelectMany(list => list);

    internal static ConfigOption[] OptionsOf(Type type)
    {
        lock (options)
        {
            if (options.TryGetValue(type, out ConfigOption[]? found))
                return found;
            var members = new List<MemberInfo>();
            members.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
            members.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            found = members
                .Where(m => m.DeclaringType != typeof(ModConfig) && m.DeclaringType!.IsSubclassOf(typeof(ModConfig)) && ConfigOption.Shown(m))
                .OrderBy(m => Depth(m.DeclaringType!)).ThenBy(m => m.MetadataToken)
                .Select(m => new ConfigOption(m))
                .ToArray();
            options[type] = found;
            return found;
        }
    }

    static int Depth(Type type)
    {
        int depth = 0;
        for (Type? t = type; t != null; t = t.BaseType)
            depth++;
        return depth;
    }

    internal static void Add(ModConfig config)
    {
        ConfigMenu.Install();
        ConfigNetHooks.Install();
        FindSavePath(config.Mod);
        Load(config);
        if (!Configs.TryGetValue(config.Mod, out List<ModConfig>? list))
            Configs[config.Mod] = list = new List<ModConfig>();
        list.Add(config);
        if (!loadTimeConfigs.TryGetValue(config.Mod, out List<ModConfig>? loadTime))
            loadTimeConfigs[config.Mod] = loadTime = new List<ModConfig>();
        loadTime.Add(config.Clone());
        config.GetType().GetField("Instance", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, config);
        try
        {
            config.OnLoaded();
        }
        catch (Exception e)
        {
            Log.Error($"{config.FullName}.OnLoaded: {e}");
        }
    }

    public static ModConfig? GetConfig(Mod mod, string config) =>
        Configs.TryGetValue(mod, out List<ModConfig>? list) ? list.FirstOrDefault(c => c.Name == config) : null;

    internal static ModConfig? GetLoadTimeConfig(Mod mod, string config) =>
        loadTimeConfigs.TryGetValue(mod, out List<ModConfig>? list) ? list.FirstOrDefault(c => c.Name == config) : null;

    internal static string FileOf(ModConfig config) => Path.Combine(ModConfigPath, $"{config.Mod.Name}_{config.Name}.json");

    internal static void ApplyDefaults(ModConfig target)
    {
        ModConfig fresh;
        try
        {
            fresh = (ModConfig)Activator.CreateInstance(target.GetType(), true)!;
        }
        catch (Exception e)
        {
            Log.Error($"{target.FullName}: the defaults could not be created: {e}");
            return;
        }

        foreach (ConfigOption option in OptionsOf(target.GetType()))
        {
            object? value = option.Get(fresh);
            if (option.HasDefault && option.Coerce(option.Default) is { } coerced)
                value = coerced;
            else if (option.Kind == ConfigOptionKind.Range && value != null)
                value = option.Coerce(value);
            option.Set(target, value);
        }
    }

    internal static void Load(ModConfig config)
    {
        ApplyDefaults(config);
        string file = FileOf(config);
        if (!File.Exists(file))
            return;
        JsonObject? data;
        try
        {
            data = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
        }
        catch (Exception e)
        {
            Log.Error($"{config.FullName}: {file} is broken ({e.Message}); using the defaults");
            return;
        }

        if (data != null)
            Populate(config, data);
    }

    internal static void Populate(ModConfig config, JsonObject data)
    {
        foreach (ConfigOption option in OptionsOf(config.GetType()))
        {
            if (!data.TryGetPropertyValue(option.JsonName, out JsonNode? node) || node == null)
                continue;
            try
            {
                object? value = option.Kind switch
                {
                    ConfigOptionKind.Toggle => node.GetValue<bool>(),
                    ConfigOptionKind.Range => option.Coerce(node.GetValue<double>()),
                    ConfigOptionKind.Choice => option.Coerce(node.GetValue<string>()),
                    ConfigOptionKind.Color => option.Coerce(node.GetValue<string>()),
                    _ => node.Deserialize(option.Type, ConfigJson.Options)
                };
                if (value != null || option.Kind == ConfigOptionKind.Data)
                    option.Set(config, value);
            }
            catch (Exception e)
            {
                Log.Warn($"{config.FullName}.{option.Name}: the saved value could not be read ({e.Message}); using the default");
            }
        }
    }

    internal static JsonObject ToJson(ModConfig config)
    {
        var data = new JsonObject();
        foreach (ConfigOption option in OptionsOf(config.GetType()))
        {
            object? value = option.Get(config);
            data[option.JsonName] = option.Kind switch
            {
                ConfigOptionKind.Toggle => JsonValue.Create((bool)value!),
                ConfigOptionKind.Range => JsonValue.Create(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)),
                ConfigOptionKind.Choice => JsonValue.Create(value?.ToString()),
                ConfigOptionKind.Color => JsonValue.Create(ConfigOption.FormatColor((Color)value!)),
                _ => JsonSerializer.SerializeToNode(value, option.Type, ConfigJson.Options)
            };
        }

        return data;
    }

    internal static void Save(ModConfig config)
    {
        string file = FileOf(config);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, ToJson(config).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Log.Error($"{config.FullName}: {file} could not be written: {e.Message}");
        }
    }

    internal static void CopyValues(ModConfig from, ModConfig to)
    {
        foreach (ConfigOption option in OptionsOf(from.GetType()))
            option.Set(to, option.Get(from));
    }

    internal static bool SameValues(ModConfig a, ModConfig b) =>
        OptionsOf(a.GetType()).All(option => option.Kind == ConfigOptionKind.Data || Equals(option.Get(a), option.Get(b)));

    internal static ConfigSaveResult SaveChanges(ModConfig active, ModConfig pending, Action<string, Color>? status, bool silent, bool broadcast)
    {
        if (active != GetConfig(active.Mod, active.Name))
            throw new InvalidOperationException("SaveChanges must be called on the active config.");
        bool pendingIsActive = pending == active;
        if (Main.gameMenu)
        {
            if (!silent)
                SoundEngine.PlaySound(SoundID.MenuOpen);
            Save(pending);
            if (!pendingIsActive)
                CopyValues(pending, active);
            ModConfig? loadTime = GetLoadTimeConfig(active.Mod, active.Name);
            if (loadTime == null || !loadTime.NeedsReload(active))
                Changed(active);
        }
        else
        {
            if (pending.Mode == ConfigScope.ServerSide && ModNet.NetMode == NetmodeID.MultiplayerClient)
            {
                status?.Invoke(Language.GetTextValue("tModLoader.ModConfigAskingServerToAcceptChanges"), new Color(255, 255, 0, 255));
                ConfigNetHooks.Request(pending, broadcast);
                if (pendingIsActive)
                    ConfigNetHooks.Revert(active);
                return ConfigSaveResult.RequestSentToServer;
            }

            ModConfig? loadTime = GetLoadTimeConfig(active.Mod, active.Name);
            if (loadTime != null && loadTime.NeedsReload(pending))
            {
                if (!silent)
                    SoundEngine.PlaySound(SoundID.MenuClose);
                status?.Invoke(Language.GetTextValue("tModLoader.ModConfigCantSaveBecauseChangesWouldRequireAReload"), new Color(255, 0, 0, 255));
                if (pendingIsActive)
                    Load(active);
                return ConfigSaveResult.NeedsReload;
            }

            if (!silent)
                SoundEngine.PlaySound(SoundID.MenuOpen);
            Save(pending);
            if (!pendingIsActive)
                CopyValues(pending, active);
            Changed(active);
            if (active.Mode == ConfigScope.ServerSide && (ModNet.NetMode & 2) != 0)
                ConfigNetHooks.Broadcast(active, broadcast);
        }

        status?.Invoke(Language.GetTextValue("tModLoader.ModConfigConfigSaved"), new Color(0, 255, 0, 255));
        return ConfigSaveResult.Success;
    }

    internal static void Changed(ModConfig config)
    {
        try
        {
            config.OnChanged();
        }
        catch (Exception e)
        {
            Log.Error($"{config.FullName}.OnChanged: {e}");
        }
    }
}
