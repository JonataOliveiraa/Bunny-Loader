using BunnyLoader.Loaders;
using Terraria;

namespace BunnyLoader;

// Um item novo. O tipo e ItemID.Count + a ordem de registro: o mesmo a cada
// boot com os mesmos mods, e o save guarda o item pelo nome ("<uid>/<Name>").
//
// O registrado e o MOLDE. Cada Item.SetDefaults do tipo roda numa copia dele
// (NewInstance) com o Item daquela chamada; a copia ainda nao fica presa ao
// item (dados por item vem depois).
public abstract class ModItem : ModType
{
    // O item desta instancia. No molde (SetStaticDefaults, Load) e nulo.
    // Por ref: a proxy e um struct, e assim `Item.DamageType = ...` (e
    // qualquer propriedade com set) escreve nela como no tModLoader, em vez
    // de cair no CS1612 de um valor temporario.
    public ref Item Item => ref item;

    Item item;

    public int Type { get; internal set; }

    // Sem extensao, relativo a pasta da entrada do mod (content/). Como no
    // tModLoader, o padrao e o namespace + nome, e o primeiro trecho (o nome
    // do mod) pode faltar no disco: "MeuMod/Items/Espada" acha
    // content/Items/Espada.png.
    public virtual string Texture => GetType().Namespace is { Length: > 0 } ns
        ? (ns + "." + Name).Replace('.', '/')
        : Name;

    // Nome e tooltip para qualquer idioma (linhas do tooltip separadas por
    // '\n'). null = as traducoes do mod (content/Localization/<cultura>.json,
    // secoes ItemName e ItemTooltip, chave Name); sem elas, o nome da classe
    // separado ("ExampleItem" -> "Example Item") e sem tooltip.
    public virtual string? DisplayName => null;

    public virtual string? Tooltip => null;

    // As propriedades do item (dano, tamanho, uso...). O jogo ja fez o
    // ResetStats e o tipo; largura e altura em 0 viram as da textura depois.
    public virtual void SetDefaults() { }

    public virtual ModItem NewInstance(Item entity)
    {
        var copy = (ModItem)MemberwiseClone();
        copy.item = entity;
        return copy;
    }

    internal sealed override void Register() => ItemLoader.Register(this);
}
