#include "boot/Boot.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "content/common/GameRefs.h"

#include <unistd.h>

#include <atomic>

namespace bl::runtime {

namespace {
std::atomic<int> g_gameTid{0};
}

int gameThreadId() { return g_gameTid.load(std::memory_order_relaxed); }

void noteGameThread() {
    int eu = static_cast<int>(gettid());
    if (g_gameTid.load(std::memory_order_relaxed) != eu) {
        g_gameTid.store(eu, std::memory_order_relaxed);
    }
}

void boot() {
    auto& a = il2cpp::api();

    if (!a.load()) {
        BL_ERROR("Falha ao carregar a API do IL2CPP. Mods desativados.");
        return;
    }
    a.thread_attach(a.domain_get());

    if (!resolveGameRefs()) {
        BL_ERROR("Versao do jogo incompativel. Mods desativados.");
        return;
    }

    // Script e mods NAO sobem aqui — quem faz isso e a sonda (Probe.cpp).
    //
    // Estavam nos dois lugares, e o resultado era pior que duplicacao: o
    // QuickJS nascia nesta thread (o hook de il2cpp_init) e os mods eram
    // avaliados na thread da sonda. O QuickJS fixa o limite de pilha a partir
    // do ponteiro de pilha de quando o runtime e criado, entao avaliar noutra
    // thread faz a checagem concluir que a pilha acabou:
    //
    //     erro em minishark: Maximum call stack size exceeded
    //
    // ...na primeira chamada, com um script de 20 linhas. Deixando uma dona so,
    // criacao e avaliacao ficam na mesma thread.
    //
    // A sonda tambem espera o jogo assentar antes de tocar no il2cpp, o que
    // aqui, dentro do proprio il2cpp_init, nao daria para fazer.

    // TODO(Fase 3): installProjectileHooks() e demais hooks de runtime.
    BL_DEBUG("runtime pronto; mods ficam a cargo da sonda");
}

} // namespace bl::runtime
