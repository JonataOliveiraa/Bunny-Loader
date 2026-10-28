O **ExampleMod do tModLoader** em **C#**, para o Bunny Loader. É o par do
Example Mod em JavaScript: cada loader C# que fica pronto ganha aqui o exemplo
dele, portado do original.

O código vai junto com o pacote (`content/*.cs`): o catálogo compila e o Bunny
Loader só confere o selo. Dá para abrir os arquivos e usar como modelo.

## O que já tem

- ![](content/Content/Items/ExampleItem.png) **Exemplo de Item**
  (`Content/Items/ExampleItem.cs`): o `ModItem` mais simples, com nome e
  descrição vindos de `Localization/<idioma>_Mods.ExampleMod.hjson`.
- ![](content/Content/Items/Weapons/Melee/ExampleMeleeWeapon.png) **Espada**
  (`Content/Items/Weapons/Melee/ExampleMeleeWeapon.cs`): dano, repulsão,
  crítico, uso e o som de golpe do jogo.

Os dois aparecem no Mod Menu, na pasta *Itens* do mod.

## Ainda não

Receitas, pesquisa do modo Jornada e o resto do ExampleMod chegam com os
loaders C# correspondentes.
