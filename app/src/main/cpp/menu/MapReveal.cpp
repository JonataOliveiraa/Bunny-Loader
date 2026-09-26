#include "menu/MapReveal.h"

namespace bl::runtime {

namespace {
constexpr uint8_t kFullLight = 255;
}

int revealMapColumns(Il2CppObject* map, const MethodInfo* updateLighting, int x, int maxX,
                     int maxY, std::chrono::steady_clock::time_point until, bool* threw) {
    using Fn = bool (*)(Il2CppObject*, int32_t, int32_t, uint8_t, const MethodInfo*);
    const auto fn = reinterpret_cast<Fn>(il2cpp::methodPointer(updateLighting));
    *threw = false;
    try {
        while (x < maxX) {
            for (int32_t y = 0; y < maxY; ++y) fn(map, x, y, kFullLight, updateLighting);
            ++x;
            if (std::chrono::steady_clock::now() >= until) break;
        }
    } catch (...) {
        *threw = true;
    }
    return x;
}

} // namespace bl::runtime
