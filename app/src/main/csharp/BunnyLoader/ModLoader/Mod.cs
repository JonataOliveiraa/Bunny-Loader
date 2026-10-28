using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BunnyLoader;
using log4net;

namespace Terraria.ModLoader;

// A classe de entrada de um mod C#: o carregador instancia cada classe nao
// abstrata que a estende, registra o conteudo do assembly e chama o Load.
//
// A ordem, como no tModLoader: o conteudo (autoload) e o Load de cada mod na
// carga; o SetStaticDefaults de cada tipo quando o jogo o conhece; depois, com
// todo o conteudo no jogo, PostSetupContent, AddRecipeGroups, AddRecipes e
// PostAddRecipes, cada passo em todos os mods (na ordem de carga) antes do
// proximo.
public abstract class Mod
{
    readonly List<ILoadable> content = new();
    ModAssets? assets;

    // So durante a carga do mod (autoload e Load): o AddContent recusa fora dela.
    internal bool loading;

    public string Uid { get; internal set; } = "";

    // O nome interno (o do assembly), o primeiro trecho do FullName do conteudo.
    public string Name => GetType().Assembly.GetName().Name ?? GetType().Name;

    // O "name" e o "version" do manifesto (0.0 se a versao nao e numerica).
    public string DisplayName { get; internal set; } = "";

    public Version Version { get; internal set; } = new(0, 0);

    // O "id" do manifesto: com o Uid e o Name, o que o ModLoader.TryGetMod aceita.
    internal string ManifestId = "";

    // O "dependencies" do manifesto (id ou uid de cada um).
    internal string[] Dependencies = Array.Empty<string>();

    // As linhas do mod no log da sessao e no logcat, com "[Name]" na frente.
    public ILog Logger { get; internal set; } = null!;

    // A pasta da entrada (content/): onde Texture e outros caminhos sao procurados.
    public string Folder { get; internal set; } = "";

    // As texturas do mod, pelo caminho dentro da pasta da entrada.
    public ModAssets Assets => assets ??= new ModAssets(this);

    // false: nenhuma classe do assembly entra sozinha; o mod chama AddContent
    // no Load. Para mudar, no construtor do mod.
    public bool ContentAutoloadingEnabled { get; init; } = true;

    // Depois do autoload do conteudo deste mod.
    public virtual void Load() { }

    // Com todo o conteudo de todos os mods no jogo (thread do jogo).
    public virtual void PostSetupContent() { }

    public virtual void AddRecipeGroups() { }

    public virtual void AddRecipes() { }

    public virtual void PostAddRecipes() { }

    // Pelo tModLoader: nao e chamado (ILoadable.Unload).
    public virtual void Unload() { }

    // O que este mod oferece a outros: ModLoader.GetMod("este").Call("Comando", ...).
    // Por convencao o primeiro argumento nomeia o comando; o desconhecido
    // devolve null. Um erro aqui chega a quem chamou.
    public virtual object? Call(params object?[] args) => null;

    // Carrega a instancia como conteudo deste mod. false se ela mesma se
    // desligou (IsLoadingEnabled). So no Load ou no autoload.
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

    // O conteudo carregado deste mod, na ordem de carga.
    public IEnumerable<ILoadable> GetContent() => content.AsReadOnly();

    public IEnumerable<T> GetContent<T>() where T : ILoadable => content.OfType<T>();

    // O conteudo deste mod pelo nome (Name, sem o do mod).
    public T Find<T>(string name) where T : IModType => ModContent.Find<T>(Name, name);

    public bool TryFind<T>(string name, out T value) where T : IModType => ModContent.TryFind(Name, name, out value);

    // Os arquivos do mod, pelo caminho dentro da pasta da entrada, com "/" e
    // extensao: "Localization/en-US.json". Nada fora dela.
    public bool FileExists(string name) => ResolveFile(name) is string path && File.Exists(path);

    public byte[] GetFileBytes(string name) => File.ReadAllBytes(ExistingFile(name));

    // Sempre um FileStream novo (o newFileStream do tModLoader nao muda nada aqui).
    public Stream GetFileStream(string name, bool newFileStream = false) => File.OpenRead(ExistingFile(name));

    public List<string> GetFileNames()
    {
        if (!Directory.Exists(Folder)) return new List<string>();
        return Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Folder, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    // O caminho no disco de um arquivo do mod; null se o nome sai da pasta.
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

    // Cada ILoadable nao abstrato do assembly com construtor sem parametros e
    // sem [Autoload(false)], na ordem do nome completo (o tipo do item nao
    // muda se o compilador reordenar).
    internal void Autoload(Assembly assembly)
    {
        if (!ContentAutoloadingEnabled) return;
        foreach (Type type in assembly.GetTypes().Where(IsAutoloaded).OrderBy(t => t.FullName, StringComparer.Ordinal))
            AddContent((ILoadable)Activator.CreateInstance(type, true)!);
    }

    static bool IsAutoloaded(Type type) =>
        !type.IsAbstract && !type.ContainsGenericParameters && typeof(ILoadable).IsAssignableFrom(type) &&
        type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null &&
        AutoloadAttribute.GetValue(type).NeedsAutoloading;
}
