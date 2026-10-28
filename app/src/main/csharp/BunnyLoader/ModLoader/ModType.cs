using System.Text.RegularExpressions;

namespace Terraria.ModLoader;

// A base do conteudo de mod (ModItem e os que vierem), no modelo do ModType do
// tModLoader: o carregador acha cada classe nao abstrata do assembly do mod,
// cria uma instancia (o molde) e a registra antes do Mod.Load.
public abstract class ModType : ILoadable
{
    public Mod Mod { get; internal set; } = null!;

    // O nome interno; com o mod, a chave estavel do save ("<uid>/<Name>").
    public virtual string Name => GetType().Name;

    public string FullName => $"{Mod?.Name ?? "Terraria"}/{Name}";

    void ILoadable.Load(Mod mod)
    {
        Mod = mod;
        InitTemplateInstance();
        Load();
        Register();
    }

    // false deixa a classe de fora (uma opcao do mod que desliga o item, por
    // exemplo). Roda antes do Load, com o Mod ainda nulo: use o parametro.
    public virtual bool IsLoadingEnabled(Mod mod) => true;

    // No registro, antes do jogo conhecer o tipo.
    public virtual void Load() { }

    // Quando o jogo ja conhece o tipo; cada loader chama o SetStaticDefaults
    // daqui. Thread do jogo.
    public virtual void SetupContent() { }

    // Quando o jogo ja conhece o tipo (os ItemID.Sets ja tem o lugar dele). Thread do jogo.
    public virtual void SetStaticDefaults() { }

    // Pelo tModLoader: nao e chamado (ILoadable.Unload).
    public virtual void Unload() { }

    public string PrettyPrintName() => Regex.Replace(Name, "([A-Z])", " $1").Trim();

    // Entrega o molde ao loader do tipo de conteudo (ItemLoader...).
    protected abstract void Register();

    // Antes do Load: o molde ganha a entidade dele (ModType<TEntity, TModType>).
    protected virtual void InitTemplateInstance() { }
}
