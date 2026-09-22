#include "runtime/Cheats.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/GameRefs.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace bl::runtime {

namespace {

struct Vector2 { float x; float y; };

// Caminho do comando escrito pelo botao do launcher (via root).
constexpr const char* kCmdPath = "/data/local/tmp/bunny/cmd";

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

bool resolveCheatRefs() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* item = findClass({"Terraria", "Item", {}});
    if (!main || !item) { BL_ERROR("cheats: Main/Item nao resolvidos"); return false; }

    g_playerField = a.class_get_field_from_name(main, "player");
    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    // Overload so-primitivo: NewItem(X,Y,Width,Height,Type,Stack,noBroadcast,pfix,noGrabDelay)
    g_newItem = a.class_get_method_from_name(item, "NewItem", 9);

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

void pollCommands() {
    FILE* f = fopen(kCmdPath, "r");
    if (!f) return;
    char buf[128] = {0};
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    fclose(f);
    remove(kCmdPath);  // executa uma vez
    if (n == 0) return;

    // formato: "give <type>"
    if (std::strncmp(buf, "give ", 5) == 0) {
        int type = std::atoi(buf + 5);
        if (type > 0) giveItem(type);
    }
}

void hkDoUpdate(Il2CppObject* self, Il2CppObject* gt, const MethodInfo* m) {
    pollCommands();
    g_origDoUpdate(self, gt, m);
}

} // namespace

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
        BL_INFO("cheats: pronto (comando em %s)", kCmdPath);
    } else {
        BL_ERROR("cheats: falha ao hookar Main.DoUpdate");
    }

    // Prova do runtime_invoke (chamar metodo do jogo): get_myPlayer() estatico.
    Il2CppObject* exc = nullptr;
    int me = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    BL_INFO("cheats: selftest runtime_invoke get_myPlayer() = %d%s",
            me, exc ? " (excecao!)" : "");
}

} // namespace bl::runtime
