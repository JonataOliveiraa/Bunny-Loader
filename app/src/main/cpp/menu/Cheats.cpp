#include <string>
#include <vector>
#include "boot/Boot.h"
#include "menu/Cheats.h"
#include "core/Config.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/common/GameRefs.h"
#include "content/common/ModContent.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "content/buffs/ModBuffs.h"
#include "content/tiles/ModTiles.h"
#include "menu/MenuCatalog.h"
#include "content/projectiles/ModProjectiles.h"
#include "menu/NetRequests.h"
#include "menu/Powers.h"
#include <atomic>
#include <unordered_map>
#include <chrono>
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

// Pedido pendente de invocar NPC. 0 = nada.
// (tipo << 32 | quantidade), como o de item. Era so o tipo, e o menu
// pedindo 10 vezes seguidas sobrescrevia o slot: saia UM NPC.
std::atomic<uint64_t> g_pendingSpawn{0};

// Pedido pendente de buff no jogador local: (tipo << 32 | segundos).
std::atomic<uint64_t> g_pendingBuff{0};

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
const MethodInfo* g_getMyPlayer = nullptr; // Main.get_myPlayer() — myPlayer e property
const MethodInfo* g_getGameMenu = nullptr; // Main.get_gameMenu(), tambem property
const MethodInfo* g_newItem = nullptr;   // Item.NewItem(9 args, tudo primitivo)
const MethodInfo* g_newNpc = nullptr;    // NPC.NewNPC(source, X, Y, Type, ...)
const MethodInfo* g_sourceCtor = nullptr;  // EntitySource_DebugCommand.ctor()
Il2CppClass* g_sourceClass = nullptr;
bool g_refsOk = false;

constexpr int kMsgSyncNpc = 23;           // MessageID.SyncNPC

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
    g_getGameMenu = a.class_get_method_from_name(main, "get_gameMenu", 0);
    if (!g_getGameMenu) BL_WARN("cheats: get_gameMenu nao encontrado; o botao fica sempre visivel");
    // Por ASSINATURA: NewItem tem quatro overloads de 9 parametros e
    // class_get_method_from_name (nome + aridade) pegava o errado, dando
    // excecao a cada frame. Antes isto era uma peneira manual iterando metodos;
    // agora e a mesma resolucao que os mods usam em JS.
    g_newItem = findMethodBySignature(item, parseSignature(
        "int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, "
        "bool noBroadcast, int pfix, bool noGrabDelay)"));

    // Invocar NPC. O NewNPC exige um IEntitySource — nao ha overload sem ele.
    // EntitySource_DebugCommand e literalmente "veio de um comando de debug",
    // tem construtor sem argumento e e o que o proprio jogo usa no console.
    if (Il2CppClass* npc = findClass({"Terraria", "NPC", {}})) {
        g_newNpc = findMethodBySignature(npc, parseSignature(
            "int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, "
            "float ai0, float ai1, float ai2, float ai3, int Target)"));
    }
    g_sourceClass = findClass({"Terraria.DataStructures", "EntitySource_DebugCommand", {}});
    if (g_sourceClass) {
        g_sourceCtor = a.class_get_method_from_name(g_sourceClass, ".ctor", 0);
    }
    if (!g_newNpc || !g_sourceCtor) {
        BL_WARN("cheats: invocar NPC indisponivel (NewNPC=%p source=%p)",
                (const void*)g_newNpc, (const void*)g_sourceCtor);
    }

    if (!g_playerField || !g_getMyPlayer || !g_newItem) {
        BL_ERROR("cheats: refs faltando (player=%p get_myPlayer=%p NewItem=%p)",
                 (void*)g_playerField, (void*)g_getMyPlayer, (void*)g_newItem);
        return false;
    }
    return true;
}

/**
 * Main.player[index], ou o jogador local com index < 0. nullptr fora do mundo
 * ou com indice fora do array (o indice pode vir da rede).
 */
Il2CppObject* playerAt(int index) {
    auto& a = il2cpp::api();
    Il2CppArray* players = nullptr;
    a.field_static_get_value(g_playerField, &players);
    if (!players) return nullptr;
    if (index < 0) {
        Il2CppObject* exc = nullptr;
        index = unboxInt(a.runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
        if (exc) return nullptr;
    }
    if (index < 0 || static_cast<uintptr_t>(index) >= players->length) return nullptr;
    return reinterpret_cast<Il2CppObject**>(arrayData(players))[index];
}

/** O jogador local, ou nullptr fora do mundo. */
Il2CppObject* localPlayer() { return playerAt(-1); }

// Comeca true: sem get_gameMenu o botao nunca apareceria.
std::atomic<bool> g_inWorld{true};

void updateInWorld() {
    if (!g_getGameMenu) return;
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(g_getGameMenu, nullptr, nullptr, &exc);
    if (exc || !r) return;
    const bool menu = *(reinterpret_cast<uint8_t*>(r) + sizeof(Il2CppObject)) != 0;
    g_inWorld.store(!menu, std::memory_order_relaxed);
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

// ----------------------- nomes de item e NPC -----------------------
//
// Do dump: ItemID.Count = 6147, NPCID.Count = 697. Sao `const` do C#, que o
// IL2CPP resolve em tempo de compilacao e nao deixa como campo para ler — por
// isso vem daqui, com o dump como fonte, do mesmo jeito que os ids do
// CheatData.
// Itens do jogo + itens de mod JA instalados; fixado quando a leitura dos nomes
// comeca (o menu so abre dentro do mundo, e os de mod entram na tela de titulo).
int g_itemTotal = kVanillaItemCount;
// NPCs do jogo + NPCs de mod ja instalados; fixado junto com g_itemTotal.
int g_npcTotal = kVanillaNpcCount;
// Quanto do quadro a leitura dos nomes pode tomar. Era uma conta fixa (250
// nomes por quadro), que num aparelho lento viraria um quadro inteiro perdido
// a cada vez; por TEMPO, o aparelho lento so demora mais para terminar. 2 ms
// sao 12% de um quadro a 60 fps.
constexpr auto kBudgetPerFrame = std::chrono::microseconds(2000);

std::vector<std::u16string> g_itemNames;
std::vector<std::u16string> g_npcNames;
std::vector<int> g_npcFrames;
const MethodInfo* g_itemNameOf = nullptr;
const MethodInfo* g_npcNameOf = nullptr;
int g_nameProgress = -1;   // -1 = nem comecou; >= g_itemTotal+g_npcTotal = pronto
// Para o log de quanto a leitura custou: do primeiro quadro ao ultimo, quantos
// quadros e o pior deles.
std::chrono::steady_clock::time_point g_nameReadStart;
int g_nameReadFrames = 0;
int64_t g_nameReadWorstUs = 0;
std::atomic<bool> g_namesReady{false};
// So mói quando alguem pede. Moer desde o primeiro DoUpdate parecia esperto e
// estava errado: naquele instante a Localization do jogo ainda nao carregou, e
// as 6800 chamadas voltavam com texto vazio — a lista saia com "#0, #1, #2".
// Quando o menu abre, o jogo ja esta de pe ha muito.
std::atomic<bool> g_namesWanted{false};

std::vector<uint8_t> g_itemClass;
std::vector<uint8_t> g_itemSub;
std::vector<int32_t> g_itemStack;

/** A secao de UM item, pelos campos que o SetDefaults preencheu. */
struct ItemFields {
    int32_t damage, pick, axe, hammer, fishingPole, ammo, notAmmo,
        melee, ranged, magic, summon, sentry, accessory, headSlot, bodySlot,
        legSlot, potion, consumable, healLife, healMana, buffType, createTile,
        createWall, maxStack;
    // Para a subcategoria (useStyle, dye e paint sao byte; os slots, sbyte).
    int32_t useStyle, shoot, useAmmo, wingSlot, shoeSlot, shieldSlot, vanity, dye, paint,
        bait, material, mountType;
};

/**
 * As tabelas do jogo que dizem o que o item E (ioio, cajado, broca...), lidas
 * uma vez no comeco da classificacao. Tabela ausente = pergunta que responde
 * "nao"; a subcategoria cai em "Outros".
 */
struct KindTables {
    Il2CppArray *staff = nullptr, *yoyo = nullptr, *drill = nullptr, *chainsaw = nullptr,
        *food = nullptr, *torches = nullptr, *bossBag = nullptr, *crate = nullptr,
        *boomerangs = nullptr, *whip = nullptr, *projHook = nullptr, *tileSolid = nullptr;
    int arrow = 40, bullet = 97, rocket = 771;   // AmmoID (estaticos, lidos no preparo)
} g_kinds;

bool flagAt(Il2CppArray* t, int i) {
    return t && i >= 0 && static_cast<uintptr_t>(i) < t->length && static_cast<const uint8_t*>(arrayData(t))[i];
}

/**
 * aiStyle do projetil `type` — e o que separa lanca (19), mangual (15) e
 * bumerangue (3), inclusive nos itens de mod. Nao ha amostra de projetil no
 * jogo: um Projectile de rascunho recebe o SetDefaults de cada tipo, uma vez.
 */
int projectileAi(int type) {
    static std::unordered_map<int, int> cache;
    static Il2CppObject* scratch = nullptr;
    static const MethodInfo* setDefaults = nullptr;
    static int32_t offAi = -1;
    static bool tried = false;
    if (type <= 0) return -1;
    auto it = cache.find(type);
    if (it != cache.end()) return it->second;
    auto& a = il2cpp::api();
    if (!tried) {
        tried = true;
        Il2CppClass* proj = il2cpp::findClass({"Terraria", "Projectile", {}});
        const MethodInfo* ctor = proj ? a.class_get_method_from_name(proj, ".ctor", 0) : nullptr;
        setDefaults = proj ? il2cpp::findMethodBySignature(
            proj, il2cpp::parseSignature("void SetDefaults(int Type)")) : nullptr;
        offAi = proj ? il2cpp::fieldOffset(proj, "aiStyle") : -1;
        if (proj && ctor && setDefaults && offAi >= 0) {
            scratch = a.object_new(proj);
            Il2CppObject* exc = nullptr;
            a.runtime_invoke(ctor, scratch, nullptr, &exc);
            if (exc) scratch = nullptr;
            else a.gchandle_new(scratch, false);   // vive para sempre, como o cache
        }
    }
    int ai = -1;
    if (scratch) {
        int t = type;
        void* args[1] = {&t};
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(setDefaults, scratch, args, &exc);
        if (!exc) ai = field<int32_t>(scratch, offAi);
    }
    cache[type] = ai;
    return ai;
}

/**
 * A ORDEM das perguntas e a decisao. Uma picareta e `melee` com dano, mas quem
 * procura espada nao quer 80 picaretas no meio; bala e `ranged` com dano, mas
 * e municao; chicote vem marcado `summon`. Por isso ferramenta e municao sao
 * perguntadas antes das classes de dano, e invocacao antes de corpo a corpo.
 */
uint8_t classOf(Il2CppObject* it, const ItemFields& o) {
    auto i32 = [&](int32_t off) { return field<int32_t>(it, off); };
    auto b = [&](int32_t off) { return field<uint8_t>(it, off) != 0; };
    const bool hasDamage = i32(o.damage) > 0;

    if (i32(o.pick) > 0 || i32(o.axe) > 0 || i32(o.hammer) > 0 || i32(o.fishingPole) > 0)
        return kClassTool;
    // Gancho e ferramenta, nao "Outros": dispara um projetil de gancho.
    if (flagAt(g_kinds.projHook, i32(o.shoot))) return kClassTool;
    // Areia e municao da Arma de Areia, mas e bloco antes de tudo.
    if (i32(o.ammo) > 0 && !b(o.notAmmo) && i32(o.createTile) < 0) return kClassAmmo;
    if (b(o.summon) || b(o.sentry)) return kClassSummon;
    if (hasDamage && b(o.melee)) return kClassMelee;
    if (hasDamage && b(o.ranged)) return kClassRanged;
    if (hasDamage && b(o.magic)) return kClassMagic;
    if (b(o.accessory)) return kClassAccessory;
    // Slot 0 e peca de verdade (FamiliarWig); "sem slot" e -1.
    if (i32(o.headSlot) >= 0 || i32(o.bodySlot) >= 0 || i32(o.legSlot) >= 0) return kClassArmor;
    if (b(o.potion) || (b(o.consumable) &&
        (i32(o.healLife) > 0 || i32(o.healMana) > 0 || i32(o.buffType) > 0)))
        return kClassPotion;
    if (i32(o.createTile) >= 0 || i32(o.createWall) > 0) return kClassBlock;
    return kClassOther;
}

/**
 * A subcategoria do item dentro da secao dele. Os numeros sao o contrato com
 * o CheatBridge.java (SUB_NAMES, por secao): 0 e sempre "Outros".
 */
uint8_t subOf(Il2CppObject* it, int id, uint8_t cls, const ItemFields& o) {
    auto i32 = [&](int32_t off) { return field<int32_t>(it, off); };
    auto i8 = [&](int32_t off) { return static_cast<int>(field<int8_t>(it, off)); };
    auto u8 = [&](int32_t off) { return static_cast<int>(field<uint8_t>(it, off)); };
    const KindTables& k = g_kinds;
    const int shoot = i32(o.shoot);
    switch (cls) {
        case kClassMelee: {
            if (flagAt(k.yoyo, id)) return 5;                          // Ioios
            const int ai = shoot > 0 ? projectileAi(shoot) : -1;
            if (flagAt(k.boomerangs, id) || ai == 3) return 6;       // Bumerangues
            if (ai == 19) return 3;                                   // Lancas
            if (ai == 15) return 4;                                   // Manguais
            if (u8(o.useStyle) == 13) return 2;                       // Espadas curtas
            if (u8(o.useStyle) == 1) return 1;                        // Espadas
            return 0;
        }
        case kClassRanged: {
            const int ammo = i32(o.useAmmo);
            if (ammo == k.arrow) return 1;                            // Arcos
            if (ammo == k.bullet) return 2;                           // Armas de fogo
            if (ammo == k.rocket) return 3;                           // Lanca-foguetes
            if (u8(o.consumable)) return 4;                           // Arremessaveis
            return 0;
        }
        case kClassMagic:
            return flagAt(k.staff, id) ? 1 : 0;                       // Cajados
        case kClassSummon:
            if (flagAt(k.whip, shoot)) return 3;                      // Chicotes
            if (u8(o.sentry)) return 2;                               // Sentinelas
            if (i32(o.buffType) > 0) return 1;                        // Lacaios
            return 0;
        case kClassAmmo: {
            const int ammo = i32(o.ammo);
            if (ammo == k.arrow) return 1;                            // Flechas
            if (ammo == k.bullet) return 2;                           // Balas
            if (ammo == k.rocket) return 3;                           // Foguetes
            return 0;
        }
        case kClassTool:
            if (flagAt(k.drill, id) || flagAt(k.chainsaw, id)) return 4;   // Brocas e serras
            if (i32(o.fishingPole) > 0) return 5;                     // Varas de pesca
            if (flagAt(k.projHook, shoot)) return 6;                  // Ganchos
            if (i32(o.pick) > 0) return 1;                            // Picaretas
            if (i32(o.axe) > 0) return 2;                             // Machados
            if (i32(o.hammer) > 0) return 3;                          // Martelos
            return 0;
        case kClassAccessory:
            if (i8(o.wingSlot) > 0) return 1;                         // Asas
            if (i8(o.shoeSlot) > 0) return 2;                         // Calcados
            if (i8(o.shieldSlot) > 0) return 3;                       // Escudos
            return 0;
        case kClassArmor:
            if (u8(o.vanity)) return 4;                               // Vaidade
            if (i32(o.headSlot) >= 0) return 1;                       // Capacetes
            if (i32(o.bodySlot) >= 0) return 2;                       // Peitorais
            if (i32(o.legSlot) >= 0) return 3;                        // Calcas
            return 0;
        case kClassPotion:
            if (i32(o.healLife) > 0) return 1;                        // Cura
            if (i32(o.healMana) > 0) return 2;                        // Mana
            if (flagAt(k.food, id)) return 3;                         // Comida
            if (i32(o.buffType) > 0) return 4;                        // Buffs
            return 0;
        case kClassBlock: {
            if (flagAt(k.torches, id)) return 3;                      // Tochas
            if (i32(o.createWall) > 0) return 2;                      // Paredes
            const int tile = i32(o.createTile);
            if (tile >= 0 && flagAt(k.tileSolid, tile)) return 1;     // Blocos
            if (tile >= 0) return 4;                                  // Moveis e objetos
            return 0;
        }
        case kClassOther:
            if (flagAt(k.bossBag, id) || flagAt(k.crate, id)) return 5;    // Bolsas e caixas
            if (i32(o.mountType) >= 0) return 4;                      // Montarias
            if (i32(o.bait) > 0) return 3;                            // Iscas
            if (u8(o.dye) > 0 || u8(o.paint) > 0) return 2;          // Tintas e corantes
            if (u8(o.material)) return 1;                             // Materiais
            return 0;
        default:
            return 0;
    }
}

Il2CppArray* staticArray(const char* ns, const char* cls, const char* nested, const char* name) {
    // Em silencio: tabela ausente e subcategoria vazia, nao erro na tela.
    Il2CppClass* c = il2cpp::findClassQuiet(ns, cls);
    if (c && nested) c = il2cpp::findNested(c, nested);
    FieldInfo* f = c ? il2cpp::findField(c, name) : nullptr;
    Il2CppArray* arr = nullptr;
    if (f) {
        il2cpp::api().runtime_class_init(c);
        il2cpp::api().field_static_get_value(f, &arr);
    }
    if (!arr) BL_WARN("cheats: %s.%s nao encontrado; essa subcategoria fica vazia", cls, name);
    return arr;
}

int staticInt(const char* ns, const char* cls, const char* name, int fallback) {
    Il2CppClass* c = il2cpp::findClassQuiet(ns, cls);
    FieldInfo* f = c ? il2cpp::findField(c, name) : nullptr;
    int v = fallback;
    if (f) il2cpp::api().field_static_get_value(f, &v);
    return v;
}

void loadKindTables() {
    KindTables& k = g_kinds;
    k.staff = staticArray("Terraria", "Item", nullptr, "staff");
    k.yoyo = staticArray("Terraria.ID", "ItemID", "Sets", "Yoyo");
    k.drill = staticArray("Terraria.ID", "ItemID", "Sets", "IsDrill");
    k.chainsaw = staticArray("Terraria.ID", "ItemID", "Sets", "IsChainsaw");
    k.food = staticArray("Terraria.ID", "ItemID", "Sets", "IsFood");
    k.torches = staticArray("Terraria.ID", "ItemID", "Sets", "Torches");
    k.bossBag = staticArray("Terraria.ID", "ItemID", "Sets", "BossBag");
    k.crate = staticArray("Terraria.ID", "ItemID", "Sets", "IsFishingCrate");
    k.boomerangs = staticArray("Terraria.GameContent.Prefixes", "PrefixLegacy", "ItemSets", "BoomerangsChakrams");
    k.whip = staticArray("Terraria.ID", "ProjectileID", "Sets", "IsAWhip");
    k.projHook = staticArray("Terraria", "Main", nullptr, "projHook");
    k.tileSolid = staticArray("Terraria", "Main", nullptr, "tileSolid");
    k.arrow = staticInt("Terraria.ID", "AmmoID", "Arrow", 40);
    k.bullet = staticInt("Terraria.ID", "AmmoID", "Bullet", 97);
    k.rocket = staticInt("Terraria.ID", "AmmoID", "Rocket", 771);
}

void classifyModItems(const ItemFields& o) {
    auto& a = il2cpp::api();
    Il2CppClass* itemCls = il2cpp::findClass({"Terraria", "Item", {}});
    const MethodInfo* ctor = itemCls ? a.class_get_method_from_name(itemCls, ".ctor", 0) : nullptr;
    const MethodInfo* setDefaults = itemCls ? il2cpp::findMethodBySignature(
        itemCls, il2cpp::parseSignature("void SetDefaults(int Type, ItemVariant variant)")) : nullptr;
    if (!ctor || !setDefaults) return;
    Il2CppObject* scratch = nullptr;
    int done = 0;
    for (int id = kVanillaItemCount; id < g_itemTotal; ++id) {
        if (g_itemClass[static_cast<size_t>(id)] != kClassNone || isUnloadedType(id)) continue;
        if (!scratch) {
            scratch = a.object_new(itemCls);
            Il2CppObject* exc = nullptr;
            a.runtime_invoke(ctor, scratch, nullptr, &exc);
            if (exc) return;
        }
        int t = id;
        void* args[2] = {&t, nullptr};
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(setDefaults, scratch, args, &exc);
        if (exc || field<int32_t>(scratch, o.maxStack) <= 0) continue;
        const uint8_t c = classOf(scratch, o);
        g_itemClass[static_cast<size_t>(id)] = c;
        g_itemSub[static_cast<size_t>(id)] = subOf(scratch, id, c, o);
        const bool unique = (c == kClassMelee || c == kClassRanged || c == kClassMagic ||
                            c == kClassSummon || c == kClassTool || c == kClassAccessory ||
                            c == kClassArmor) && !field<uint8_t>(scratch, o.consumable);
        g_itemStack[static_cast<size_t>(id)] = unique ? 1 : field<int32_t>(scratch, o.maxStack);
        ++done;
    }
    if (done) BL_INFO("cheats: %d item(ns) de mod classificado(s)", done);
}

/**
 * Le ContentSamples.ItemsByType (Dictionary<int, Item>) em fatias.
 *
 * Direto nas entradas do dicionario, sem `get_Item` por id: sao ~6000 itens e
 * o dicionario ja esta montado na memoria desde o boot. Os nomes dos campos
 * internos (`_entries`, `_count`, `key`, `value`) sao resolvidos pelo il2cpp,
 * como tudo o mais — so o LAYOUT de uma entrada e deduzido, porque o offset de
 * campo de struct pode vir contando o cabecalho de objeto.
 *
 * Em fatias, no mesmo orcamento dos nomes: de uma vez eram 9,4 ms num quadro
 * so no MuMu, um tranco visivel. A resolucao fica guardada entre um quadro e
 * outro; o array de entradas e relido a cada fatia, para nunca segurar um
 * ponteiro que o jogo tenha trocado no meio.
 */
struct Classification {
    bool failed = false;
    FieldInfo* fDict = nullptr;
    int32_t offEntries = -1, offCount = -1;
    size_t stride = 0, oKey = 0, oVal = 0, oHash = 0;
    ItemFields o{};
    int32_t pos = -1;   // -1 = ainda nao preparada
    int32_t readCount = 0;
} g_class;

void failClassification(const char* reason) {
    BL_ERROR("cheats: %s; as secoes de item ficam vazias (Todos os itens continua)", reason);
    g_class.failed = true;
}

/** O dicionario e o array de entradas AGORA. nullptr se sumiram. */
Il2CppArray* dictionaryEntries(int32_t* count) {
    auto& a = il2cpp::api();
    Il2CppObject* dict = nullptr;
    a.field_static_get_value(g_class.fDict, &dict);
    if (!dict) return nullptr;
    *count = field<int32_t>(dict, g_class.offCount);
    return field<Il2CppArray*>(dict, g_class.offEntries);
}

bool prepareClassification() {
    using namespace il2cpp;
    auto& a = api();
    Classification& k = g_class;

    Il2CppClass* cs = findClass({"Terraria.ID", "ContentSamples", {}});
    k.fDict = cs ? findField(cs, "ItemsByType") : nullptr;
    Il2CppObject* dict = nullptr;
    if (k.fDict) a.field_static_get_value(k.fDict, &dict);
    Il2CppClass* itemCls = findClass({"Terraria", "Item", {}});
    if (!dict || !itemCls) { failClassification("ContentSamples.ItemsByType nao encontrado"); return false; }

    Il2CppClass* dc = a.object_get_class(dict);
    FieldInfo* fEntries = a.class_get_field_from_name(dc, "_entries");
    FieldInfo* fCount = a.class_get_field_from_name(dc, "_count");
    if (!fEntries || !fCount) { failClassification("layout do Dictionary diferente do esperado"); return false; }
    k.offEntries = static_cast<int32_t>(a.field_get_offset(fEntries));
    k.offCount = static_cast<int32_t>(a.field_get_offset(fCount));

    int32_t count = 0;
    Il2CppArray* entries = dictionaryEntries(&count);
    Il2CppClass* ec = entries ? a.class_get_element_class(
                                    a.object_get_class(reinterpret_cast<Il2CppObject*>(entries)))
                              : nullptr;
    if (!ec) { failClassification("entradas do dicionario sem tipo"); return false; }
    FieldInfo* fKey = a.class_get_field_from_name(ec, "key");
    FieldInfo* fVal = a.class_get_field_from_name(ec, "value");
    FieldInfo* fHash = a.class_get_field_from_name(ec, "hashCode");
    if (!fKey || !fVal || !fHash) { failClassification("Entry sem key/value/hashCode"); return false; }

    uint32_t align = 0;
    k.stride = static_cast<size_t>(a.class_value_size(ec, &align));
    k.oKey = a.field_get_offset(fKey);
    k.oVal = a.field_get_offset(fVal);
    k.oHash = a.field_get_offset(fHash);
    // `value` e um ponteiro e e o ultimo campo: se ele "termina" depois do
    // tamanho da entrada, os offsets estao contando o cabecalho de objeto.
    if (k.oVal + sizeof(void*) > k.stride) {
        k.oKey -= sizeof(Il2CppObject);
        k.oVal -= sizeof(Il2CppObject);
        k.oHash -= sizeof(Il2CppObject);
    }

    ItemFields& o = k.o;
    struct { int32_t* dst; const char* name; } fields[] = {
        {&o.damage, "damage"}, {&o.pick, "pick"}, {&o.axe, "axe"},
        {&o.hammer, "hammer"}, {&o.fishingPole, "fishingPole"}, {&o.ammo, "ammo"},
        {&o.notAmmo, "notAmmo"}, {&o.melee, "melee"}, {&o.ranged, "ranged"},
        {&o.magic, "magic"}, {&o.summon, "summon"}, {&o.sentry, "sentry"},
        {&o.accessory, "accessory"}, {&o.headSlot, "headSlot"}, {&o.bodySlot, "bodySlot"},
        {&o.legSlot, "legSlot"}, {&o.potion, "potion"}, {&o.consumable, "consumable"},
        {&o.healLife, "healLife"}, {&o.healMana, "healMana"}, {&o.buffType, "buffType"},
        {&o.createTile, "createTile"}, {&o.createWall, "createWall"}, {&o.maxStack, "maxStack"},
        {&o.useStyle, "useStyle"}, {&o.shoot, "shoot"}, {&o.useAmmo, "useAmmo"},
        {&o.wingSlot, "wingSlot"}, {&o.shoeSlot, "shoeSlot"}, {&o.shieldSlot, "shieldSlot"},
        {&o.vanity, "vanity"}, {&o.dye, "dye"}, {&o.paint, "paint"}, {&o.bait, "bait"},
        {&o.material, "material"}, {&o.mountType, "mountType"},
    };
    for (auto& c : fields) {
        *c.dst = fieldOffset(itemCls, c.name);
        if (*c.dst < 0) {
            BL_ERROR("cheats: Item.%s nao encontrado; as secoes de item ficam vazias", c.name);
            k.failed = true;
            return false;
        }
    }

    g_itemClass.assign(g_itemTotal, kClassNone);
    g_itemSub.assign(g_itemTotal, 0);
    g_itemStack.assign(g_itemTotal, 0);
    loadKindTables();
    k.pos = 0;
    return true;
}

/**
 * Classifica ate `limite`. true quando acabou (ou falhou: ai as secoes ficam
 * vazias e o menu segue so com "Todos").
 */
template <typename TimePoint>
bool classifyUntil(TimePoint deadline) {
    Classification& k = g_class;
    if (k.failed) return true;
    if (k.pos < 0 && !prepareClassification()) return true;

    int32_t count = 0;
    Il2CppArray* entries = dictionaryEntries(&count);
    if (!entries) { failClassification("o dicionario de amostras sumiu no meio"); return true; }
    const char* base = static_cast<const char*>(arrayData(entries));
    const ItemFields& o = k.o;
    for (; k.pos < count && static_cast<uintptr_t>(k.pos) < entries->length; ++k.pos) {
        if ((k.pos & 63) == 0 && k.pos > 0 && std::chrono::steady_clock::now() > deadline) {
            return false;
        }
        const char* ent = base + static_cast<size_t>(k.pos) * k.stride;
        // Entrada liberada tem hashCode -1 (o de uma valida e mascarado para
        // ficar >= 0). Este dicionario nunca perde item, mas nao custa.
        if (*reinterpret_cast<const int32_t*>(ent + k.oHash) < 0) continue;
        const int32_t id = *reinterpret_cast<const int32_t*>(ent + k.oKey);
        auto* item = *reinterpret_cast<Il2CppObject* const*>(ent + k.oVal);
        if (!item || id <= 0 || id >= g_itemTotal) continue;
        const uint8_t c = classOf(item, o);
        g_itemClass[id] = c;
        g_itemSub[id] = subOf(item, id, c, o);
        // O maxStack nao separa nada nesta versao: o ResetStats poe
        // Item.CommonMaxStack (9999) em TODO item, espada inclusive, e o jogo
        // aceita uma Lamina da Terra com pilha 999 no inventario (visto no
        // MuMu). Quem nao empilha e decidido pela secao: arma, ferramenta,
        // acessorio e armadura saem um so — menos o que se gasta ao usar,
        // como faca de arremesso e granada.
        const bool unique = (c == kClassMelee || c == kClassRanged || c == kClassMagic ||
                            c == kClassSummon || c == kClassTool || c == kClassAccessory ||
                            c == kClassArmor) && !field<uint8_t>(item, o.consumable);
        g_itemStack[id] = unique ? 1 : field<int32_t>(item, o.maxStack);
        ++k.readCount;
    }

    // Item de mod nao tem amostra no ContentSamples: um Item de rascunho recebe
    // o SetDefaults de cada um (o do mod roda junto) e e classificado igual.
    classifyModItems(o);

    int perClass[11] = {};
    for (uint8_t c : g_itemClass) if (c < 11) ++perClass[c];
    BL_INFO("cheats: %d itens classificados | corpo %d, distancia %d, magia %d, "
            "invocacao %d, municao %d, ferramenta %d, acessorio %d, armadura %d, "
            "pocao %d, bloco %d, outros %d", k.readCount, perClass[1], perClass[2],
            perClass[3], perClass[4], perClass[5], perClass[6], perClass[7],
            perClass[8], perClass[9], perClass[10], perClass[0]);
    return true;
}

std::u16string toU16(Il2CppString* s) {
    if (!s || s->length <= 0) return {};
    return std::u16string(reinterpret_cast<const char16_t*>(s->chars),
                          static_cast<size_t>(s->length));
}

/**
 * Uma fatia por quadro, no orcamento: primeiro os nomes, depois as classes.
 * Volta rapido quando ja acabou.
 */
void readNamesSlice() {
    if (!g_namesWanted.load(std::memory_order_relaxed)) return;
    if (g_namesReady.load(std::memory_order_relaxed)) return;
    auto& a = il2cpp::api();

    if (g_nameProgress < 0) {
        Il2CppClass* lang = il2cpp::findClass({"Terraria", "Lang", {}});
        g_itemNameOf = lang ? a.class_get_method_from_name(lang, "GetItemNameValue", 1) : nullptr;
        g_npcNameOf  = lang ? a.class_get_method_from_name(lang, "GetNPCNameValue", 1) : nullptr;
        if (!g_itemNameOf || !g_npcNameOf) {
            BL_ERROR("cheats: Lang.GetItemNameValue/GetNPCNameValue nao encontrados; "
                     "o menu fica so com os ids");
            g_namesReady.store(true, std::memory_order_release);
            return;
        }
        g_itemTotal = itemTypeCount();
        // A reserva de "?" (item de mod ausente) e a ultima faixa de ids e nao
        // e item que se pega: fica fora do menu.
        while (g_itemTotal > kVanillaItemCount && isUnloadedType(g_itemTotal - 1)) --g_itemTotal;
        g_itemNames.assign(g_itemTotal, std::u16string());
        g_npcTotal = npcTypeCount();
        g_npcNames.assign(g_npcTotal, std::u16string());
        g_nameProgress = 0;
        g_nameReadStart = std::chrono::steady_clock::now();
    }

    using Clock = std::chrono::steady_clock;
    const auto t0 = Clock::now();
    const int total = g_itemTotal + g_npcTotal;
    for (; g_nameProgress < total; ++g_nameProgress) {
        // O relogio a cada 16 nomes, e nao a cada um: perguntar a hora tambem
        // custa. O primeiro lote sempre passa, entao todo quadro anda.
        if ((g_nameProgress & 15) == 0 && g_nameProgress > 0 &&
            Clock::now() - t0 > kBudgetPerFrame) {
            break;
        }
        const bool item = g_nameProgress < g_itemTotal;
        int id = item ? g_nameProgress : g_nameProgress - g_itemTotal;
        void* args[1] = {&id};
        Il2CppObject* exc = nullptr;
        Il2CppObject* r = a.runtime_invoke(item ? g_itemNameOf : g_npcNameOf,
                                           nullptr, args, &exc);
        if (exc) continue;   // id sem nome: fica vazio, o menu mostra so o id
        std::u16string name = toU16(reinterpret_cast<Il2CppString*>(r));
        if (item) g_itemNames[id] = std::move(name);
        else g_npcNames[id] = std::move(name);
    }
    // Nomes prontos: o que sobra do orcamento vai para a classificacao, que
    // tambem para no limite e continua no proximo quadro.
    const bool classified = g_nameProgress >= total && classifyUntil(t0 + kBudgetPerFrame);
    ++g_nameReadFrames;
    const int64_t us = std::chrono::duration_cast<std::chrono::microseconds>(
        Clock::now() - t0).count();
    if (us > g_nameReadWorstUs) g_nameReadWorstUs = us;
    if (classified) {
        // Main.npcFrameCount, de uma vez: sao 697 ints ja prontos na memoria do
        // jogo, nao ha o que fatiar.
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        FieldInfo* f = main ? il2cpp::findField(main, "npcFrameCount") : nullptr;
        Il2CppArray* arr = nullptr;
        if (f) a.field_static_get_value(f, &arr);
        if (arr) {
            const int32_t* v = static_cast<const int32_t*>(arrayData(arr));
            size_t n = static_cast<size_t>(arr->length);
            g_npcFrames.assign(v, v + n);
        } else {
            BL_ERROR("cheats: Main.npcFrameCount nao veio; a lista vai mostrar a "
                     "tira inteira de cada NPC");
        }
        // Subcategorias de NPC e a aba de buffs: juntos, antes de dizer pronto.
        buildMenuCatalogExtras();
        g_namesReady.store(true, std::memory_order_release);
        int unnamed = 0;
        for (const auto& n : g_itemNames) if (n.empty()) ++unnamed;
        const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            Clock::now() - g_nameReadStart).count();
        BL_INFO("cheats: nomes e classes prontos (%d itens, %d NPCs; %d itens sem "
                "nome) em %lld ms, %d quadros, pior quadro %.1f ms", g_itemTotal, g_npcTotal,
                unnamed, static_cast<long long>(ms), g_nameReadFrames, g_nameReadWorstUs / 1000.0);
    }
}

/**
 * O buff `type` no jogador local, por `seconds`. No multijogador o proprio
 * AddBuff do jogo avisa o servidor (o jogador e dono dos buffs dele).
 */
void giveBuff(int type, int seconds) {
    auto& a = il2cpp::api();
    static const MethodInfo* addBuff = nullptr;
    if (!addBuff) {
        Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
        addBuff = player ? il2cpp::findMethodBySignature(
            player, il2cpp::parseSignature("void AddBuff(int type, int time, bool fromNetPvP)")) : nullptr;
        if (!addBuff) { BL_ERROR("cheats: Player.AddBuff nao encontrado"); return; }
    }
    Il2CppObject* p = playerAt(-1);
    if (!p) return;
    int t = type, time = seconds * 60;
    uint8_t fromNet = 0;
    void* args[3] = {&t, &time, &fromNet};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(addBuff, p, args, &exc);
    if (exc) BL_ERROR("cheats: AddBuff(%d) lancou excecao", type);
}

void hkDoUpdate(Il2CppObject* self, Il2CppObject* gt, const MethodInfo* m) {
    // Daqui sai a identidade da thread do jogo: e a unica em que se pode criar
    // objeto de Unity (ver bl.loadTexture).
    runtime::noteGameThread();
    runSelftestOnce();
    updateInWorld();
    readNamesSlice();
    tickPowers();
    tickModItems();
    tickModProjectiles();
    tickModNpcs();
    tickModBuffs();
    tickModTiles();
    tickContentReady();
    tickNetRequests();
    // Pedido do botao (in-process): consome e executa na thread do jogo.
    if (uint64_t req = g_pendingGive.exchange(0)) {
        giveItem(static_cast<int>(req >> 32), static_cast<int>(req & 0xffffffffu));
    }
    if (uint64_t req = g_pendingBuff.exchange(0)) {
        giveBuff(static_cast<int>(req >> 32), static_cast<int>(req & 0xffffffffu));
    }
    if (uint64_t req = g_pendingSpawn.exchange(0)) {
        int npcType = static_cast<int>(req >> 32);
        int spawnCount = static_cast<int>(req & 0xffffffffu);
        for (int i = 0; i < spawnCount; ++i) spawnNpc(npcType);
    }
    pollCommands();  // canal por arquivo (dev/adb) continua valendo
    g_origDoUpdate(self, gt, m);
}

} // namespace

bool namesReady() {
    // Pedir e o gatilho: quem pergunta e o menu abrindo, e ai o jogo ja carregou
    // a Localization. Quem nunca abre o menu nao paga nada.
    g_namesWanted.store(true, std::memory_order_relaxed);
    return g_namesReady.load(std::memory_order_acquire);
}
const std::vector<std::u16string>& itemNames() { return g_itemNames; }
const std::vector<std::u16string>& npcNames() { return g_npcNames; }
const std::vector<int>& npcFrames() { return g_npcFrames; }
bool inWorld() { return g_inWorld.load(std::memory_order_relaxed); }
const std::vector<uint8_t>& itemClasses() { return g_itemClass; }
const std::vector<uint8_t>& itemSubClasses() { return g_itemSub; }

void requestGive(int type, int stack) {
    if (type > 0) g_pendingGive.store(packGive(type, stack > 0 ? stack : 1));
}

void requestBuff(int type, int seconds) {
    if (type <= 0) return;
    if (seconds < 1) seconds = 1;
    g_pendingBuff.store((static_cast<uint64_t>(type) << 32) | static_cast<uint32_t>(seconds));
}

void requestSpawn(int type, int count) {
    if (count < 1) count = 1;
    if (count > 10) count = 10;   // teto do slider; 10 chefes ja e demais
    if (type > 0) {
        g_pendingSpawn.store((static_cast<uint64_t>(type) << 32) |
                             static_cast<uint32_t>(count));
    }
}

void spawnNpc(int type, int player) {
    auto& a = il2cpp::api();
    if (!g_refsOk || !g_newNpc || !g_sourceCtor) {
        BL_ERROR("cheats: invocar NPC indisponivel");
        return;
    }
    // No cliente, NewNPC cria o NPC so no Main.npc DELE: ninguem mais ve, mas
    // ele bate no jogador (o dano ao jogador e sincronizado). Quem cria NPC e
    // o servidor, entao o cliente pede (ver NetRequests).
    const int mode = netMode();
    if (mode == 1) {
        sendToServer("npc " + std::to_string(type));
        return;
    }
    Il2CppObject* p = playerAt(player);
    if (!p) { BL_WARN("spawnNpc: sem jogador %d (fora do mundo?)", player); return; }

    // Uma fonte so, reaproveitada: criar a cada invocacao geraria lixo por
    // nada, e ela nao guarda estado.
    static Il2CppObject* source = nullptr;
    if (!source) {
        source = a.object_new(g_sourceClass);
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(g_sourceCtor, source, nullptr, &exc);
        if (exc) { BL_ERROR("spawnNpc: ctor da fonte lancou excecao"); source = nullptr; return; }
    }

    Vector2 pos = field<Vector2>(p, game().entity.position);
    // Um pouco ao lado e acima: em cima do jogador o NPC nasce preso nele.
    int x = static_cast<int>(pos.x) + 160;
    int y = static_cast<int>(pos.y) - 80;
    int t = type, start = 0, target = 255;
    float ai0 = 0, ai1 = 0, ai2 = 0, ai3 = 0;
    void* args[10] = { source, &x, &y, &t, &start, &ai0, &ai1, &ai2, &ai3, &target };

    Il2CppObject* exc = nullptr;
    int idx = unboxInt(a.runtime_invoke(g_newNpc, nullptr, args, &exc));
    if (exc) { BL_ERROR("spawnNpc: NewNPC lancou excecao (type=%d)", type); return; }
    // No servidor (NetHost), o NewNPC so marca spawnNeedsSyncing e o NPC chega aos
    // clientes na proxima rodada de sincronizacao. Mandar agora evita o
    // intervalo em que ele existe so para quem hospeda.
    if ((mode & 2) && idx >= 0 && idx < 200) sendData(kMsgSyncNpc, idx);
    BL_INFO("spawnNpc: type=%d em (%d,%d) -> npc[%d] (netMode %d, jogador %d)",
            type, x, y, idx, mode, player);
}

void giveItem(int type, int stack, int player) {
    using namespace il2cpp;
    if (!g_refsOk) { BL_ERROR("cheats: refs nao prontas"); return; }
    auto& a = api();

    // No cliente, o item criado pelo NewItem nao chegava ao inventario (o
    // servidor e o dono dos itens do mundo). Pede a ele, como o NPC.
    if (netMode() == 1) {
        sendToServer("item " + std::to_string(type) + " " + std::to_string(stack));
        return;
    }
    Il2CppObject* p = playerAt(player);
    if (!p) { BL_WARN("giveItem: sem jogador %d (fora do mundo?)", player); return; }

    Vector2 pos = field<Vector2>(p, game().entity.position);
    int x = static_cast<int>(pos.x), y = static_cast<int>(pos.y);
    int w = 10, h = 10, t = type, n = stack > 0 ? stack : 1, pfix = 0;
    // O NewItem aceita qualquer pilha, e 999 espadas viravam UMA espada com
    // pilha 999. O teto vem da classificacao (ver classifyUntil).
    if (type > 0 && static_cast<size_t>(type) < g_itemStack.size() &&
        g_itemStack[type] > 0 && n > g_itemStack[type]) {
        n = g_itemStack[type];
    }
    uint8_t noBroadcast = 0, noGrab = 0;
    void* args[9] = { &x, &y, &w, &h, &t, &n, &noBroadcast, &pfix, &noGrab };

    Il2CppObject* exc = nullptr;
    int idx = unboxInt(a.runtime_invoke(g_newItem, nullptr, args, &exc));
    if (exc) { BL_ERROR("giveItem: NewItem lancou excecao"); return; }
    BL_INFO("giveItem: type=%d x%d dado em (%d,%d) -> item[%d] (jogador %d)", t, n, x, y, idx, player);
}

void installCheats() {
    installNetRequests();
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
