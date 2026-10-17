#pragma once
#include <string>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Poeiras de mod (o ModDust do tModLoader): tipos NOVOS depois do
 * DustID.Count (332), com textura propria ou a folha do jogo.
 *
 * O Dust do celular difere do PC em dois pontos que mandam no desenho disto:
 *   - as tabelas por tipo (Dust.StaticAlpha/StaticAlphaValue,
 *     ChildSafety.SafeDust) sao lidas SEM conferir o limite (o GetAlpha e o
 *     UpdateDust foram compilados sem checagem): crescem antes do 1o tipo;
 *   - as poeiras livres moram numa pilha (Dust.freeDust/freeDustCount), e so o
 *     Dust.SetNotActive devolve uma poeira a ela. O `dust.active = false` que
 *     os mods do tModLoader escrevem direto deixa a vaga fora da pilha para
 *     sempre: com o tempo nenhuma poeira nasce. A pilha e conferida e refeita
 *     (repairDustPool) — vale tambem para mod sem ModDust.
 *
 * O tModLoader poe os ganchos no meio do Dust.UpdateDust e do Main.DrawDust;
 * aqui eles ficam em volta do metodo inteiro, uma vez por quadro: as poeiras
 * que o jogo nao deve tocar ficam `active = false` durante o original e voltam
 * depois (nenhum dos dois devolve a pilha uma poeira que ja esta inativa).
 */

/** DustID.Count do jogo (campo estatico; conferido na instalacao). */
constexpr int kVanillaDustCount = 332;

/** O que o mod sobrescreveu: o JS so e chamado para isto. */
enum DustHook : unsigned {
    kDustOnSpawn = 1u << 0,
    kDustUpdate = 1u << 1,
    kDustMidUpdate = 1u << 2,
    kDustGetAlpha = 1u << 3,
    kDustPreDraw = 1u << 4,
};

struct ModDustDef {
    std::string mod;       // uid do mod que registrou
    std::string name;      // nome da classe
    std::string texture;   // caminho absoluto do PNG; "" = a folha do jogo (TextureAssets.Dust)
    unsigned hooks = 0;    // DustHook
};

/** Reserva o proximo tipo. THREAD-SAFE. -1 se o mesmo mod ja registrou esse nome. */
int registerModDust(ModDustDef def);

bool isModDust(int type);
int modDustTypeByName(const std::string& mod, const std::string& name);

/** O ModDust.UpdateType: o tipo do jogo cujo comportamento a poeira copia (-1 = nenhum). */
void setModDustUpdateType(int type, int updateType);

/** O Asset<Texture2D> do tipo (o da folha do jogo para textura ""), ou nullptr antes da instalacao. */
Il2CppObject* modDustTextureAsset(int type);

/**
 * O lado do JS (thread do jogo, com a trava do motor). `dusts` sao os indices
 * em Main.dust. Cada funcao devolve, por poeira, se segue o caminho do jogo.
 */
struct DustCallbacks {
    void (*spawn)(int type, Il2CppObject* dust) = nullptr;
    // false: o jogo nao atualiza a poeira neste quadro (Update devolveu false).
    void (*update)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& vanilla) = nullptr;
    // true: desfaz o freio horizontal (velocity.X *= 0.99) do jogo.
    void (*midUpdate)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& keepSpeed) = nullptr;
    // false: o jogo nao desenha a poeira (PreDraw devolveu false). O lote e aberto pelo JS.
    void (*preDraw)(const std::vector<Il2CppObject*>& dusts, std::vector<bool>& draw) = nullptr;
    // Cor por cima da do jogo (RGBA empacotado em `color`); false = a do jogo.
    bool (*getAlpha)(Il2CppObject* dust, uint32_t& color) = nullptr;
};
void setDustCallbacks(const DustCallbacks& callbacks);

/** Thread do jogo, a cada quadro: tabelas, texturas e hooks quando o jogo estiver pronto. */
void tickModDusts();
bool modDustsSettled();

/** Os numeros da pilha de poeiras livres (testes). */
struct DustPoolStats {
    int active = 0;          // poeiras com active = true em Main.dust[0..6000)
    int activeCount = 0;     // Dust.activeDustCount
    int freeCount = 0;       // Dust.freeDustCount
    int repairs = 0;         // vezes que a pilha foi refeita desde o boot
    int modActive = 0;       // poeiras de mod ativas
    // Tempo nos hooks desde o boot (ns): o Dust.UpdateDust inteiro, o
    // Main.DrawDust inteiro (com as passadas) e, deles, o que foi JS.
    int64_t updateNs = 0, drawNs = 0, jsNs = 0;
    int64_t updates = 0, draws = 0;
    int64_t groupPasses = 0, groupDusts = 0;   // passadas de textura propria e poeiras nelas
};
DustPoolStats dustPoolStats();

/** Confere a pilha agora e refaz se estiver errada. true se refez. */
bool repairDustPool();

} // namespace bl::runtime
