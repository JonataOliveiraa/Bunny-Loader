#include "hook/HookManager.h"
#include "core/Log.h"
#include "shadowhook.h"
#include <mutex>
#include <vector>

namespace bl::hook {

namespace {

/**
 * Todos os hooks de UM endereco.
 *
 * O ShadowHook roda em modo UNIQUE: um endereco aceita uma substituicao so, e
 * o segundo pedido e recusado. Era o que acontecia com Vida Cheia + Sem Queda,
 * os dois em Player.Update — o segundo mod ligado simplesmente nao carregava.
 *
 * Em vez de pedir de novo ao ShadowHook, o segundo hook entra NA CADEIA: ele
 * passa a ser o "original" do anterior, e o original dele e a funcao real.
 *
 *     jogo -> prologo patcheado -> R1 -> R2 -> ... -> Rn -> funcao real
 *
 * Todos os Ri hookam a mesma funcao, entao tem a mesma ABI e qualquer um pode
 * chamar o proximo como se fosse o original. Acrescentar um elo e trocar UM
 * ponteiro (o `original` de Rn-1): nada e desinstalado, nenhum trampolim do
 * ShadowHook e liberado com alguem possivelmente executando dentro dele.
 */
struct Chain {
    void* address = nullptr;
    void* stub = nullptr;             // o que o shadowhook_unhook quer
    std::vector<void**> originals;    // o `original` de cada elo, de fora pra dentro
};

std::mutex g_mutex;

std::vector<Chain>& chains() {
    static std::vector<Chain> list;
    return list;
}

} // namespace

bool install(void* address, void* replacement, void** original) {
    if (!address || !replacement || !original) return false;
    std::lock_guard<std::mutex> guard(g_mutex);

    for (Chain& c : chains()) {
        if (c.address != address) continue;
        // O novo elo aponta para onde o ultimo apontava (a funcao real, via
        // trampolim) ANTES de ser publicado: quem ler o ponteiro do ultimo elo
        // a partir de agora ja encontra um elo pronto para seguir.
        void** last = c.originals.back();
        __atomic_store_n(original, __atomic_load_n(last, __ATOMIC_ACQUIRE), __ATOMIC_RELEASE);
        __atomic_store_n(last, replacement, __ATOMIC_RELEASE);
        c.originals.push_back(original);
        BL_DEBUG("hook encadeado em %p (%zu no mesmo metodo)", address, c.originals.size());
        return true;
    }

    void* stub = shadowhook_hook_func_addr(address, replacement, original);
    if (!stub) {
        BL_ERROR("Hook falhou em %p: %s", address,
                 shadowhook_to_errmsg(shadowhook_get_errno()));
        return false;
    }
    chains().push_back({address, stub, {original}});
    return true;
}

bool install(const MethodInfo* method, void* replacement, void** original) {
    if (!method) return false;
    return install(il2cpp::methodPointer(method), replacement, original);
}

void removeAll() {
    std::lock_guard<std::mutex> guard(g_mutex);
    for (Chain& c : chains()) shadowhook_unhook(c.stub);
    chains().clear();
}

} // namespace bl::hook
