using System.Text.RegularExpressions;

namespace BunnyLoader;

// A base do conteudo de mod (ModItem e os que vierem), no modelo do ModType do
// tModLoader: o carregador acha cada classe nao abstrata do assembly do mod,
// cria uma instancia (o molde) e a registra antes do Mod.Load.
public abstract class ModType
{
    public Mod Mod { get; internal set; } = null!;

    // O nome interno; com o mod, a chave estavel do save ("<uid>/<Name>").
    public virtual string Name => GetType().Name;

    public string FullName => $"{Mod?.Name ?? "Terraria"}/{Name}";

    // false deixa a classe de fora (uma opcao do mod que desliga o item, por exemplo).
    public virtual bool IsLoadingEnabled(Mod mod) => true;

    // No registro, antes do jogo conhecer o tipo.
    public virtual void Load() { }

    // Quando o jogo ja conhece o tipo (os ItemID.Sets ja tem o lugar dele). Thread do jogo.
    public virtual void SetStaticDefaults() { }

    public string PrettyPrintName() => Regex.Replace(Name, "([A-Z])", " $1").Trim();

    internal abstract void Register();
}
