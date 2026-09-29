#include "content/tiles/ModWater.h"
#include "core/Log.h"
#include "hook/CodePatch.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

namespace bl::runtime {

namespace {

// O primeiro laco anda pelo endereco do elemento em floats: o indice + 8
// (os 32 bytes do cabecalho do array).
constexpr int kHeaderInFloats = 8;

int g_count = kVanillaWaterStyleCount;

} // namespace

bool setWaterStyleCount(int total) {
    if (total <= g_count) return true;
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    const MethodInfo* m = main ? a.class_get_method_from_name(main, "DrawWaters", 1) : nullptr;
    if (!m) {
        BL_ERROR("agua de mod: Main.DrawWaters nao achado");
        return false;
    }
    const auto from = static_cast<uint32_t>(g_count), to = static_cast<uint32_t>(total);
    const int fade = patchLoopEnd(m, from + kHeaderInFloats, to + kHeaderInFloats);
    const int draw = patchLoopEnd(m, from, to, /*exitOnEqual*/ true);
    if (fade != 1 || draw != 1) {
        BL_ERROR("agua de mod: lacos do Main.DrawWaters nao casaram (fade %d, desenho %d; esperado 1 e 1): %s",
                 fade, draw, describeMethodCode(m).c_str());
        return false;
    }
    BL_INFO("agua de mod: Main.DrawWaters anda por %d estilos (eram %d)", total, g_count);
    g_count = total;
    return true;
}

} // namespace bl::runtime
