namespace Terraria.ModLoader;

// O que o carregador sabe carregar: todo ModType, e qualquer classe do mod que
// queira entrar na carga (o Mod.AddContent, ou o autoload do assembly).
public interface ILoadable
{
    // false deixa a instancia de fora: nem Load, nem registro.
    bool IsLoadingEnabled(Mod mod) => true;

    void Load(Mod mod);

    // Pelo tModLoader. O Bunny Loader nao descarrega mods durante a sessao (o
    // jogo reinicia para trocar de mods), entao isto nao e chamado.
    void Unload();
}
