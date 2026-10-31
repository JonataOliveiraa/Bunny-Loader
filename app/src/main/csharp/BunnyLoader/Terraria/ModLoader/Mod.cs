using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BunnyLoader;
using log4net;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class Mod
{
    readonly List<ILoadable> content = new();
    ModAssets? assets;

    internal bool loading;

    public string Uid { get; internal set; } = "";

    public string Name => GetType().Assembly.GetName().Name ?? GetType().Name;

    public string DisplayName { get; internal set; } = "";

    public Version Version { get; internal set; } = new(0, 0);

    internal string ManifestId = "";

    internal string[] Dependencies = Array.Empty<string>();

    public ILog Logger { get; internal set; } = null!;

    public string Folder { get; internal set; } = "";

    public ModAssets Assets => assets ??= new ModAssets(this);

    public bool ContentAutoloadingEnabled { get; init; } = true;

    public virtual void Load() { }

    public virtual void PostSetupContent() { }

    public virtual void AddRecipeGroups() { }

    public virtual void AddRecipes() { }

    public virtual void PostAddRecipes() { }

    public Recipe CreateRecipe(int result, int amount = 1) => Recipe.Create(result, amount);

    public virtual void Unload() { }

    public virtual object? Call(params object?[] args) => null;

    public ModPacket GetPacket(int capacity = 256) => new(this, capacity);

    public int AddBossHeadTexture(string texture, int npcType = -1) => NPCLoader.ReserveBossHead(this, texture, npcType);

    public virtual void HandlePacket(BinaryReader reader, int whoAmI) { }

    public bool AddContent(ILoadable instance)
    {
        if (!loading) throw new InvalidOperationException($"{Name}: AddContent can only be called while the mod loads (Mod.Load)");
        if (!instance.IsLoadingEnabled(this)) return false;
        instance.Load(this);
        content.Add(instance);
        ModContent.Add(instance);
        return true;
    }

    public bool AddContent<T>() where T : ILoadable, new() => AddContent(new T());

    public IEnumerable<ILoadable> GetContent() => content.AsReadOnly();

    public IEnumerable<T> GetContent<T>() where T : ILoadable => content.OfType<T>();

    public string GetLocalizationKey(string suffix) => $"Mods.{Name}.{suffix}";

    public LocalizedText GetLocalization(string suffix, Func<string>? makeDefaultValue = null) =>
        Language.GetOrRegister(GetLocalizationKey(suffix), makeDefaultValue);

    public T Find<T>(string name) where T : IModType => ModContent.Find<T>(Name, name);

    public bool TryFind<T>(string name, out T value) where T : IModType => ModContent.TryFind(Name, name, out value);

    public bool FileExists(string name) => ResolveFile(name) is string path && File.Exists(path);

    public byte[] GetFileBytes(string name) => File.ReadAllBytes(ExistingFile(name));

    public Stream GetFileStream(string name, bool newFileStream = false) => File.OpenRead(ExistingFile(name));

    public List<string> GetFileNames()
    {
        if (!Directory.Exists(Folder)) return new List<string>();
        return Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Folder, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    internal string? ResolveFile(string name)
    {
        if (Folder.Length == 0 || string.IsNullOrEmpty(name)) return null;
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Folder));
        string path = Path.GetFullPath(Path.Combine(root, name.Replace('\\', '/').TrimStart('/')));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }

    string ExistingFile(string name)
    {
        string? path = ResolveFile(name);
        if (path == null || !File.Exists(path)) throw new FileNotFoundException($"{Name}: no file '{name}' in the mod", name);
        return path;
    }

    internal void Autoload(Assembly assembly)
    {
        if (!ContentAutoloadingEnabled) return;
        foreach (Type type in assembly.GetTypes().Where(IsAutoloaded).OrderBy(t => t.FullName, StringComparer.Ordinal))
            AddContent((ILoadable)Activator.CreateInstance(type, true)!);
        GoreLoader.AutoloadGores(this);
    }

    static bool IsAutoloaded(Type type) =>
        !type.IsAbstract && !type.ContainsGenericParameters && typeof(ILoadable).IsAssignableFrom(type) &&
        type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null &&
        AutoloadAttribute.GetValue(type).NeedsAutoloading;
}
