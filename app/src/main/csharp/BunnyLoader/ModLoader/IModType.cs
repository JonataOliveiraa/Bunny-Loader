namespace Terraria.ModLoader;

// O conteudo que tem nome: o que ModContent.Find e Mod.Find acham.
public interface IModType
{
    Mod Mod { get; }

    string Name { get; }

    // "<nome do mod>/<Name>".
    string FullName { get; }
}
