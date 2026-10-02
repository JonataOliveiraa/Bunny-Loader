// Usa a implementacao real e controla os estados sem carregar o Terraria.
#include "content/tiles/ModTiles.cpp"

#include <cstdio>

int main() {
    using namespace bl::runtime;
    struct Case {
        const char* name;
        int total, installed;
        bool staticDone, failed, ready;
    };
    const Case cases[] = {
        {"sem tiles registrados", 0, 0, false, false, true},
        {"tiles ainda nao instalados", 1, 0, false, false, false},
        {"lote parcialmente instalado", 2, 1, true, false, false},
        {"SetStaticDefaults pendente", 1, 1, false, false, false},
        {"lote pronto", 1, 1, true, false, true},
        {"falha definitiva nao bloqueia boot", 1, 0, false, true, true},
    };
    for (const Case& test : cases) {
        g_total.store(test.total);
        g_installed.store(test.installed);
        g_staticDone.store(test.staticDone);
        g_failed = test.failed;
        if (modTilesSettled() != test.ready) {
            std::fprintf(stderr, "FAIL: %s\n", test.name);
            return 1;
        }
        std::printf("PASS: %s\n", test.name);
    }
    return 0;
}
