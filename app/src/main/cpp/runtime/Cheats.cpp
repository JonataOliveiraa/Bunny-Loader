#include "runtime/Cheats.h"
#include "core/Config.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
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
// Empacotado num unico atomico (type << 32 | stack) para que tipo e quantidade
// sejam publicados juntos — com dois atomicos o frame poderia ler o tipo novo
// com a quantidade velha.
std::atomic<uint64_t> g_pendingGive{0};

uint64_t packGive(int type, int stack) {
    return (static_cast<uint64_t>(static_cast<uint32_t>(type)) << 32) |
           static_cast<uint32_t>(stack);
}

// Canal de DEV por arquivo (adb push). O do BOTAO e requestGive(), em processo.
//
// O caminho vem da config, nao fixo no codigo: ele apontava para a pasta
// externa do Terraria, de quando rodavamos dentro do processo dele. Agora o
// processo e o nosso, e desde o Android 11 um app nao escreve em Android/data
// de outro — o arquivo nunca chegaria.
const std::string& cmdPath() { return config().cmdPath; }

// Refs resolvidas uma vez.
FieldInfo* g_playerField = nullptr;      // Terraria.Main.player  (Player[])
FieldInfo* g_itemArrayField = nullptr;   // Terraria.Main.item    (Item[]) — diagnostico
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
    g_itemArrayField = a.class_get_field_from_name(main, "item");  // opcional
    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    // Por ASSINATURA: NewItem tem quatro overloads de 9 parametros e
    // class_get_method_from_name (nome + aridade) pegava o errado, dando
    // excecao a cada frame. Antes isto era uma peneira manual iterando metodos;
    // agora e a mesma resolucao que os mods usam em JS.
    g_newItem = findMethodBySignature(item, parseSignature(
        "int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, "
        "bool noBroadcast, int pfix, bool noGrabDelay)"));

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

    // formato: "give <type> [stack]"
    if (std::strncmp(buf, "give ", 5) == 0) {
        char* end = nullptr;
        int type = static_cast<int>(std::strtol(buf + 5, &end, 10));
        int stack = end ? static_cast<int>(std::strtol(end, nullptr, 10)) : 0;
        if (type > 0) giveItem(type, stack > 0 ? stack : 1);
    }
}

// Diagnostico: o item recem-criado saiu com os defaults aplicados?
//
// Motivado por "itens do cheat vem sem tooltip, so o nome". Um item cujo
// SetDefaults rodou tem damage/useTime/maxStack preenchidos; tudo zero aponta
// para o item ter sido criado e nao inicializado. Le Main.item[idx] por offset
// de campo, sem chamar nada do jogo.
void describeSpawned(int idx) {
    using namespace il2cpp;
    if (!g_itemArrayField || idx < 0) return;
    auto& a = api();

    Il2CppArray* items = nullptr;
    a.field_static_get_value(g_itemArrayField, &items);
    if (!items) return;
    auto** elems = reinterpret_cast<Il2CppObject**>(arrayData(items));
    Il2CppObject* it = elems[idx];
    if (!it) { BL_WARN("giveItem: item[%d] nulo apos NewItem", idx); return; }

    Il2CppClass* cls = a.object_get_class(it);
    auto readInt = [&](const char* name) -> int {
        FieldInfo* f = findField(cls, name);
        if (!f) return -1;
        return *reinterpret_cast<int32_t*>(
            reinterpret_cast<char*>(it) + a.field_get_offset(f));
    };
    BL_INFO("giveItem: item[%d] type=%d stack=%d maxStack=%d damage=%d useTime=%d",
            idx, readInt("type"), readInt("stack"), readInt("maxStack"),
            readInt("damage"), readInt("useTime"));
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
    if (uint64_t req = g_pendingGive.exchange(0)) {
        giveItem(static_cast<int>(req >> 32), static_cast<int>(req & 0xffffffffu));
    }
    pollCommands();  // canal por arquivo (dev/adb) continua valendo
    g_origDoUpdate(self, gt, m);
}

} // namespace

void requestGive(int type, int stack) {
    if (type > 0) g_pendingGive.store(packGive(type, stack > 0 ? stack : 1));
}

void giveItem(int type, int stack) {
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
    int w = 10, h = 10, t = type, n = stack > 0 ? stack : 1, pfix = 0;
    uint8_t noBroadcast = 0, noGrab = 0;
    void* args[9] = { &x, &y, &w, &h, &t, &n, &noBroadcast, &pfix, &noGrab };

    Il2CppObject* exc = nullptr;
    int idx = unboxInt(a.runtime_invoke(g_newItem, nullptr, args, &exc));
    if (exc) { BL_ERROR("giveItem: NewItem lancou excecao"); return; }
    BL_INFO("giveItem: type=%d x%d dado em (%d,%d) -> item[%d]", t, n, x, y, idx);
    describeSpawned(idx);
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
