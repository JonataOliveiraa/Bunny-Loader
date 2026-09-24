#pragma once
#include <string>
#include <vector>

namespace bl::runtime {

/**
 * O catalogo de conteudo de mod no Mod Menu: uma entrada por mod, e dentro
 * dela PASTAS — como o catalogo do TL Pro.
 *
 * Toda pasta e de itens ou de NPCs. Cada mod ganha sozinho "Itens" e "NPCs"
 * (tudo que registrou de cada tipo, na ordem) e pode criar as suas com
 * bl.menu.itemCategory / bl.menu.npcCategory. THREAD-SAFE: mods registram na
 * thread da sonda, o menu le na da UI.
 */
struct ModMenuFolder {
    std::string mod;       // uid do mod
    std::string modName;   // nome do manifesto
    std::string modIcon;   // icon.png do mod (caminho absoluto), ou vazio
    std::string name;      // nome da pasta
    std::string icon;      // PNG que o mod deu a pasta, ou vazio (usa o 1o da lista)
    bool npc = false;
    std::vector<int> types;
};

/** Nome e icone do mod no menu. Chamado a cada registro; o primeiro nao vazio fica. */
void setModMenuInfo(const std::string& mod, const std::string& name, const std::string& icon);

/** Pasta criada pelo mod. Devolve o id dela (o mesmo para o mesmo nome e tipo). */
int addModMenuFolder(const std::string& mod, const std::string& name, const std::string& icon, bool npc);

/** Poe `type` na pasta `folder`. false se a pasta nao existe. */
bool addToModMenuFolder(int folder, int type);

/**
 * Todas as pastas, agrupadas por mod na ordem em que os mods apareceram: as
 * automaticas ("Itens", "NPCs", so se tiverem algo) e depois as do mod. Copia.
 */
std::vector<ModMenuFolder> modMenuFolders();

} // namespace bl::runtime
