#include "boot/FramePacing.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <atomic>
#include <cstdint>

namespace bl::runtime {

namespace {

using SetTargetFn = void (*)(int32_t, const MethodInfo*);
SetTargetFn g_origSetTarget = nullptr;

const MethodInfo* g_getTarget60 = nullptr;   // Main.get_Setting_Target60FPS
const MethodInfo* g_setTarget60 = nullptr;   // Main.set_Setting_Target60FPS
std::atomic<bool> g_reapply{false};

// Quem chama: XNAUnityRunner.Awake e SetAndroidSurfaceRate, e o
// Main.SetPlatform60FPS (60, ou 30 com a economia de bateria).
void hkSetTargetFrameRate(int32_t value, const MethodInfo* m) {
    g_origSetTarget(value == 60 ? 61 : value, m);
}

} // namespace

void installFramePacing() {
    auto& a = il2cpp::api();
    Il2CppClass* app = il2cpp::findClass({"UnityEngine", "Application", {}});
    const MethodInfo* set = app ? a.class_get_method_from_name(app, "set_targetFrameRate", 1) : nullptr;
    if (!set || !hook::install(set, hkSetTargetFrameRate, &g_origSetTarget)) {
        BL_ERROR("quadros: Application.set_targetFrameRate nao hookado; numa tela de 90 Hz o jogo fica em 45 fps");
        return;
    }
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    g_getTarget60 = main ? a.class_get_method_from_name(main, "get_Setting_Target60FPS", 0) : nullptr;
    g_setTarget60 = main ? a.class_get_method_from_name(main, "set_Setting_Target60FPS", 1) : nullptr;
    // O jogo pode ja ter pedido 60 antes do hook (o Awake roda antes da sonda).
    g_reapply.store(g_getTarget60 && g_setTarget60, std::memory_order_release);
}

void tickFramePacing() {
    if (!g_reapply.exchange(false, std::memory_order_acq_rel)) return;
    using GetFn = bool (*)(const MethodInfo*);
    using SetFn = void (*)(bool, const MethodInfo*);
    const bool target60 = reinterpret_cast<GetFn>(il2cpp::methodPointer(g_getTarget60))(g_getTarget60);
    // O setter sai sem fazer nada quando o valor nao muda: vai e volta, e o
    // jogo refaz a escolha inteira (com a economia de bateria) pelo hook.
    const SetFn set = reinterpret_cast<SetFn>(il2cpp::methodPointer(g_setTarget60));
    set(!target60, g_setTarget60);
    set(target60, g_setTarget60);
    BL_INFO("quadros: alvo refeito pelo jogo (Setting_Target60FPS = %s; 60 vira 61)", target60 ? "sim" : "nao");
}

} // namespace bl::runtime
