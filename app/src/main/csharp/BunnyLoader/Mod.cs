using System;
using System.Linq;
using System.Reflection;

namespace BunnyLoader;

// A classe de entrada de um mod C#: o carregador instancia cada classe nao
// abstrata que a estende, registra o conteudo do assembly e chama o Load.
public abstract class Mod
{
    public string Uid { get; internal set; } = "";

    // O nome interno (o do assembly), o primeiro trecho do FullName do conteudo.
    public string Name => GetType().Assembly.GetName().Name ?? GetType().Name;

    // A pasta da entrada (content/): onde Texture e outros caminhos sao procurados.
    public string Folder { get; internal set; } = "";

    public virtual void Load() { }

    // Cada ModType nao abstrato do assembly vira um molde registrado, na ordem
    // do nome completo (o tipo do item nao muda se o compilador reordenar).
    internal void Autoload(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes().Where(IsContent).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var content = (ModType)Activator.CreateInstance(type)!;
            if (!content.IsLoadingEnabled(this)) continue;
            content.Mod = this;
            content.Load();
            content.Register();
        }
    }

    static bool IsContent(Type type) =>
        !type.IsAbstract && !type.ContainsGenericParameters && typeof(ModType).IsAssignableFrom(type) &&
        type.GetConstructor(Type.EmptyTypes) != null;
}
