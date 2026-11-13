#include "menu/DevTools.h"

#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "menu/Cheats.h"
#include "menu/NetRequests.h"
#include "hook/HookManager.h"

#include <atomic>
#include <chrono>

namespace bl::runtime {

namespace {

std::atomic<int> g_request{0};
std::atomic<bool> g_ready{false};
std::atomic<bool> g_frozen{false};

using CanPauseFn = bool (*)(const MethodInfo*);
CanPauseFn g_origCanPause = nullptr;

bool hkCanPauseGame(const MethodInfo* m) {
    if (g_origCanPause(m)) return true;
    return g_frozen.load(std::memory_order_relaxed) && netMode() == 0;
}

bool invokeStatic(const MethodInfo* m, void** args, const char* what, Il2CppObject** ret = nullptr) {
    if (!m) { BL_ERROR("restart: %s not found", what); return false; }
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, nullptr, args, &exc);
    if (exc) { BL_ERROR("reiniciar: %s lancou excecao", what); return false; }
    if (ret) *ret = r;
    return true;
}

void saveAll() {
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    if (!main || !player || !worldFile) { BL_ERROR("restart: save classes not found"); return; }

    const auto t0 = std::chrono::steady_clock::now();
    Il2CppObject* data = nullptr;
    invokeStatic(a.class_get_method_from_name(main, "get_ActivePlayerFileData", 0), nullptr,
                 "Main.ActivePlayerFileData", &data);
    if (data) {
        uint8_t skipMap = 0, force = 0;
        void* args[3] = {data, &skipMap, &force};
        invokeStatic(a.class_get_method_from_name(player, "SavePlayer", 3), args, "Player.SavePlayer");
    }

    int32_t netMode = 0;
    if (FieldInfo* f = il2cpp::findField(main, "netMode")) a.field_static_get_value(f, &netMode);
    if (netMode != 1) {
        int32_t context = 0;
        void* args[1] = {&context};
        invokeStatic(a.class_get_method_from_name(worldFile, "SaveWorld", 1), args, "WorldFile.SaveWorld");
    }
    const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - t0).count();
    BL_INFO("restart: player%s saved in %lld ms", netMode != 1 ? " and world" : "",
            static_cast<long long>(ms));
}

}

void requestRestart(bool save) {
    g_ready.store(false, std::memory_order_release);
    g_request.store(save ? 2 : 1, std::memory_order_release);
}

bool restartReady() { return g_ready.load(std::memory_order_acquire); }

void installDevTools() {
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    const MethodInfo* canPause = main ? il2cpp::api().class_get_method_from_name(main, "CanPauseGame", 0) : nullptr;
    if (!canPause || !hook::install(canPause, hkCanPauseGame, &g_origCanPause)) {
        BL_ERROR("editor: Main.CanPauseGame not hooked; the full screen does not freeze the game");
    }
}

void setGameFrozen(bool on) {
    if (g_frozen.exchange(on, std::memory_order_relaxed) != on) {
        BL_DEBUG("editor: game %s", on ? "frozen (full screen)" : "running again");
    }
}

void tickRestart() {
    const int request = g_request.exchange(0, std::memory_order_acq_rel);
    if (!request) return;
    if (request == 2 && inWorld()) saveAll();
    BL_INFO("restarting the game from the Mod Menu: the mods are read again from the folder");
    g_ready.store(true, std::memory_order_release);
}

}
