#include "menu/DevTools.h"

#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "menu/Cheats.h"

#include <atomic>
#include <chrono>

namespace bl::runtime {

namespace {

// 0 nada pedido; 1 reiniciar; 2 salvar e reiniciar.
std::atomic<int> g_request{0};
std::atomic<bool> g_ready{false};

bool invokeStatic(const MethodInfo* m, void** args, const char* what, Il2CppObject** ret = nullptr) {
    if (!m) { BL_ERROR("reiniciar: %s nao encontrado", what); return false; }
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, nullptr, args, &exc);
    if (exc) { BL_ERROR("reiniciar: %s lancou excecao", what); return false; }
    if (ret) *ret = r;
    return true;
}

/**
 * O que o "Salvar e Sair" do jogo salva: o jogador (com o mapa) e, quem nao e
 * cliente de multijogador, o mundo. Pelos metodos do jogo, entao os saves de
 * conteudo de mod (itens, tiles, moradores, ModPlayer) vao junto, como sempre.
 * Na thread do jogo, de uma vez: o quadro para o tempo do save.
 */
void saveAll() {
    auto& a = il2cpp::api();
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    if (!main || !player || !worldFile) { BL_ERROR("reiniciar: classes do save nao encontradas"); return; }

    const auto t0 = std::chrono::steady_clock::now();
    Il2CppObject* data = nullptr;
    invokeStatic(a.class_get_method_from_name(main, "get_ActivePlayerFileData", 0), nullptr,
                 "Main.ActivePlayerFileData", &data);
    if (data) {
        uint8_t skipMap = 0, force = 0;
        void* args[3] = {data, &skipMap, &force};
        invokeStatic(a.class_get_method_from_name(player, "SavePlayer", 3), args, "Player.SavePlayer");
    }

    // netMode e um campo de bits no celular: 1 = cliente (o mundo e do host).
    int32_t netMode = 0;
    if (FieldInfo* f = il2cpp::findField(main, "netMode")) a.field_static_get_value(f, &netMode);
    if (netMode != 1) {
        int32_t context = 0;   // WorldFile.WorldSaveContext.Normal
        void* args[1] = {&context};
        invokeStatic(a.class_get_method_from_name(worldFile, "SaveWorld", 1), args, "WorldFile.SaveWorld");
    }
    const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - t0).count();
    BL_INFO("reiniciar: jogador%s salvos em %lld ms", netMode != 1 ? " e mundo" : "",
            static_cast<long long>(ms));
}

} // namespace

void requestRestart(bool save) {
    g_ready.store(false, std::memory_order_release);
    g_request.store(save ? 2 : 1, std::memory_order_release);
}

bool restartReady() { return g_ready.load(std::memory_order_acquire); }

void tickRestart() {
    const int request = g_request.exchange(0, std::memory_order_acq_rel);
    if (!request) return;
    if (request == 2 && inWorld()) saveAll();
    BL_INFO("reiniciando o jogo pelo Mod Menu: os mods sao lidos de novo da pasta");
    g_ready.store(true, std::memory_order_release);
}

} // namespace bl::runtime
