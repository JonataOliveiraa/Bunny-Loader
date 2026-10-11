#include "menu/MapReveal.h"

#include "il2cpp/GuardedCall.h"

namespace bl::runtime {

namespace {
constexpr uint8_t kFullLight = 255;
}

int revealMapColumns(Il2CppObject* map, const MethodInfo* updateLighting, int x, int maxX,
                     int maxY, std::chrono::steady_clock::time_point until, bool* threw) {
    using Fn = bool (*)(Il2CppObject*, int32_t, int32_t, uint8_t, const MethodInfo*);
    const auto fn = reinterpret_cast<Fn>(il2cpp::methodPointer(updateLighting));
    // Uma guarda para o laco todo, nao uma por celula: o laco nao tem nada a
    // limpar, e `x` fica na coluna em que parou.
    auto loop = [&] {
        while (x < maxX) {
            for (int32_t y = 0; y < maxY; ++y) fn(map, x, y, kFullLight, updateLighting);
            ++x;
            if (std::chrono::steady_clock::now() >= until) break;
        }
    };
    *threw = !il2cpp::guarded(loop);
    return x;
}

} // namespace bl::runtime
