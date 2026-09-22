#include "runtime/Cheats.h"
#include "core/Config.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/GameRefs.h"
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace bl::runtime {

namespace {

struct Vector2 { float x; float y; };

// Pedido pendente de "dar item", setado de qualquer thread (UI) e consumido na
// thread do jogo pelo hook de DoUpdate. 0 = nada pendente.
std::atomic<int> g_pendingGive{0};

// Canal de DEV por arquivo (adb push). O do BOTAO e requestGive(), em processo.
//
// O caminho vem da config, nao fixo no codigo: ele apontava para a pasta
// externa do Terraria, de quando rodavamos dentro do processo dele. Agora o
// processo e o nosso, e desde o Android 11 um app nao escreve em Android/data
// de outro — o arquivo nunca chegaria.
const std::string& cmdPath() { return config().cmdPath; }

// Refs resolvidas uma vez.
FieldInfo* g_playerField = nullptr;      // Terraria.Main.player  (Player[])
const MethodInfo* g_getMyPlayer = nullptr; // Main.get_myPlayer() — myPlayer e property
const MethodInfo* g_newItem = nullptr;   // Item.NewItem(9 args, tudo primitivo)
bool g_refsOk = false;

// Desempacota um int boxed (retorno de runtime_invoke). O valor vem logo apos
// o cabecalho do objeto.
int unboxInt(Il2CppObject* boxed) {
    if (!boxed) return 0;
    return *reinterpret_cast<int*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
}

// Item.NewItem tem varios overloads com 9 parametros (um so-primitivo, outros
// comecam com IEntitySource/Vector2). class_get_method_from_name so casa por
// nome+contagem, entao iteramos e pegamos aquele cujo 1o parametro e int.
const MethodInfo* findPrimitiveNewItem(Il2CppClass* item) {
    using namespace il2cpp;
    auto& a = api();
    void* iter = nullptr;
    while (const MethodInfo* m = a.class_get_methods(item, &iter)) {
        if (std::strcmp(a.method_get_name(m), "NewItem") != 0) continue;
        if (a.method_get_param_count(m) != 9) continue;
        char* pname = a.type_get_name(a.method_get_param(m, 0));
        bool isInt = pname && std::strcmp(pname, "System.Int32") == 0;
        if (pname) a.il2cpp_free(pname);
        if (isInt) return m;
    }
    return nullptr;
}

bool resolveCheatRefs() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* item = findClass({"Terraria", "Item", {}});
    if (!main || !item) { BL_ERROR("cheats: Main/Item nao resolvidos"); return false; }

    g_playerField = a.class_get_field_from_name(main, "player");
    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    // Overload so-primitivo: NewItem(X,Y,Width,Height,Type,Stack,noBroadcast,pfix,noGrabDelay)
    g_newItem = findPrimitiveNewItem(item);

    if (!g_playerField || !g_getMyPlayer || !g_newItem) {
        BL_ERROR("cheats: refs faltando (player=%p get_myPlayer=%p NewItem=%p)",
                 (void*)g_playerField, (void*)g_getMyPlayer, (void*)g_newItem);
        return false;
    }
    return true;
}

// DoUpdate hook: le o arquivo de comando a cada frame e despacha.
using DoUpdateFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
DoUpdateFn g_origDoUpdate = nullptr;

char g_lastCmd[128] = {0};  // trava anti-repeticao caso remove() falhe (dono != app)

void pollCommands() {
    const std::string& path = cmdPath();
    if (path.empty()) return;  // canal de dev desligado
    FILE* f = fopen(path.c_str(), "r");
    if (!f) return;
    char buf[128] = {0};
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    fclose(f);
    if (n == 0) { g_lastCmd[0] = 0; return; }

    // Se remove() falhar (arquivo de outro dono), nao reexecuta o mesmo comando.
    remove(path.c_str());
    if (std::strcmp(buf, g_lastCmd) == 0) return;
    std::strncpy(g_lastCmd, buf, sizeof(g_lastCmd) - 1);

    // formato: "give <type>"
    if (std::strncmp(buf, "give ", 5) == 0) {
        int type = std::atoi(buf + 5);
        if (type > 0) giveItem(type);
    }
}

// Prova de que runtime_invoke chama codigo do jogo. Roda no PRIMEIRO DoUpdate,
// nao no installCheats: la estamos logo apos o il2cpp_init, Terraria.Main ainda
// nao rodou, e get_myPlayer() so podia estourar. O selftest antigo reportava
// "(excecao!)" sempre e por isso nao provava nada.
void runSelftestOnce() {
    static bool done = false;
    if (done) return;
    done = true;

    Il2CppObject* exc = nullptr;
    int me = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    if (exc) BL_ERROR("cheats: selftest runtime_invoke get_myPlayer() lancou excecao");
    else BL_INFO("cheats: selftest runtime_invoke get_myPlayer() = %d (ok)", me);
}

void hkDoUpdate(Il2CppObject* self, Il2CppObject* gt, const MethodInfo* m) {
    runSelftestOnce();
    // Pedido do botao (in-process): consome e executa na thread do jogo.
    if (int type = g_pendingGive.exchange(0)) giveItem(type);
    pollCommands();  // canal por arquivo (dev/adb) continua valendo
    g_origDoUpdate(self, gt, m);
}

} // namespace

void requestGive(int type) {
    if (type > 0) g_pendingGive.store(type);
}

void giveItem(int type) {
    using namespace il2cpp;
    if (!g_refsOk) { BL_ERROR("cheats: refs nao prontas"); return; }
    auto& a = api();

    Il2CppArray* players = nullptr;
    a.field_static_get_value(g_playerField, &players);
    Il2CppObject* exc0 = nullptr;
    int me = unboxInt(a.runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc0));
    if (!players) { BL_WARN("giveItem: sem array de players (fora do mundo?)"); return; }

    auto** elems = reinterpret_cast<Il2CppObject**>(arrayData(players));
    Il2CppObject* p = elems[me];
    if (!p) { BL_WARN("giveItem: player[%d] nulo (fora do mundo?)", me); return; }

    Vector2 pos = field<Vector2>(p, game().entity.position);
    int x = static_cast<int>(pos.x), y = static_cast<int>(pos.y);
    int w = 10, h = 10, t = type, stack = 1, pfix = 0;
    uint8_t noBroadcast = 0, noGrab = 0;
    void* args[9] = { &x, &y, &w, &h, &t, &stack, &noBroadcast, &pfix, &noGrab };

    Il2CppObject* exc = nullptr;
    a.runtime_invoke(g_newItem, nullptr, args, &exc);
    if (exc) BL_ERROR("giveItem: NewItem lancou excecao");
    else BL_INFO("giveItem: type=%d dado em (%d,%d)", t, x, y);
}

void installCheats() {
    g_refsOk = resolveCheatRefs();
    if (!g_refsOk) { BL_ERROR("cheats: desabilitado (refs faltando)"); return; }

    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    const MethodInfo* doUpdate = main
        ? il2cpp::api().class_get_method_from_name(main, "DoUpdate", 1) : nullptr;
    if (doUpdate && hook::install(doUpdate, hkDoUpdate, &g_origDoUpdate)) {
        // O canal do BOTAO e requestGive() -> g_pendingGive, em processo. O
        // arquivo abaixo e so o canal de dev (adb), por isso vem marcado.
        const std::string& cmd = cmdPath();
        BL_INFO("cheats: pronto (canal dev por arquivo: %s)",
                cmd.empty() ? "desligado" : cmd.c_str());
    } else {
        BL_ERROR("cheats: falha ao hookar Main.DoUpdate");
    }
}

} // namespace bl::runtime
