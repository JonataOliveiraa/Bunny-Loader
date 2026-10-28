using System.Text.RegularExpressions;

namespace Terraria.ModLoader;

public abstract class ModType : IModType, ILoadable
{
    public Mod Mod { get; internal set; } = null!;

    public virtual string Name => GetType().Name;

    public string FullName => $"{Mod?.Name ?? "Terraria"}/{Name}";

    void ILoadable.Load(Mod mod)
    {
        Mod = mod;
        InitTemplateInstance();
        Load();
        Register();
    }

    public virtual bool IsLoadingEnabled(Mod mod) => true;

    public virtual void Load() { }

    public virtual void SetupContent() { }

    public virtual void SetStaticDefaults() { }

    public virtual void Unload() { }

    public string PrettyPrintName() => Regex.Replace(Name, "([A-Z])", " $1").Trim();

    protected abstract void Register();

    protected virtual void InitTemplateInstance() { }
}
