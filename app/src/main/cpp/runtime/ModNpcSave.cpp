#include "runtime/ModNpcSave.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "runtime/GameRefs.h"
#include "runtime/ModNpcs.h"

#include <cstdio>
#include <cstdlib>
#include <map>
#include <mutex>
#include <string>
#include <vector>

namespace bl::runtime {

namespace {

constexpr const char* kSuffix = ".npcs.bl";
constexpr const char* kHeader = "bunny-npcs 1";

struct SavedNpc {
    std::string key;           // "<uid>/<nome>"
    float x = 0, y = 0;
    int homeless = 1, homeX = 0, homeY = 0, variation = 0;
    std::string name;          // nome proprio (GivenName)
};
struct SavedRoom {
    std::string key;
    int x = 0, y = 0;
};

std::mutex g_mx;
// Por mundo: as linhas de mod nao carregado (voltam ao arquivo como estavam).
std::map<std::string, std::vector<std::string>> g_kept;

struct Point { int32_t x, y; };

struct Refs {
    bool ok = false;
    FieldInfo* activeWorld = nullptr;   // Main.ActiveWorldFileData
    int32_t path = -1, cloud = -1;      // FileData._path / _isCloudSave
    FieldInfo* npcs = nullptr;          // Main.npc
    FieldInfo* shimmered = nullptr;     // NPC.ShimmeredTownNPCs
    FieldInfo* savesAndLoads = nullptr; // NPCID.Sets.SavesAndLoads
    FieldInfo* townManager = nullptr;   // WorldGen.TownManager
    int32_t active = -1, type = -1, townNPC = -1, position = -1, homeless = -1;
    int32_t homeX = -1, homeY = -1, variation = -1, givenName = -1;
    const MethodInfo* setDefaults = nullptr;
    const MethodInfo* setGivenName = nullptr;
    const MethodInfo* hasRoom = nullptr;
    const MethodInfo* kickOut = nullptr;
    const MethodInfo* setRoom = nullptr;
};
Refs g_refs;

bool resolve() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* fileData = findClass({"Terraria.IO", "FileData", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    Il2CppClass* sets = findClass({"Terraria.ID", "NPCID", "Sets"});
    Il2CppClass* worldGen = findClass({"Terraria", "WorldGen", {}});
    Il2CppClass* rooms = findClass({"Terraria.GameContent", "TownRoomManager", {}});
    if (!main || !fileData || !npc || !sets || !worldGen || !rooms) return false;
    Refs& r = g_refs;
    r.activeWorld = findField(main, "ActiveWorldFileData");
    r.path = fieldOffset(fileData, "_path");
    r.cloud = fieldOffset(fileData, "_isCloudSave");
    r.npcs = findField(main, "npc");
    r.shimmered = findField(npc, "ShimmeredTownNPCs");
    r.savesAndLoads = findField(sets, "SavesAndLoads");
    r.townManager = findField(worldGen, "TownManager");
    r.active = fieldOffset(npc, "active");
    r.type = fieldOffset(npc, "type");
    r.townNPC = fieldOffset(npc, "townNPC");
    r.position = fieldOffset(npc, "position");
    r.homeless = fieldOffset(npc, "homeless");
    r.homeX = fieldOffset(npc, "homeTileX");
    r.homeY = fieldOffset(npc, "homeTileY");
    r.variation = fieldOffset(npc, "townNpcVariationIndex");
    r.givenName = fieldOffset(npc, "_givenName");
    r.setDefaults = findMethodBySignature(npc, parseSignature("void SetDefaults(int Type, NPCSpawnParams spawnparams)"));
    r.setGivenName = a.class_get_method_from_name(npc, "set_GivenName", 1);
    r.hasRoom = findMethodBySignature(rooms, parseSignature("bool HasRoom(int npcID, out Point roomPosition)"));
    r.kickOut = findMethodBySignature(rooms, parseSignature("void KickOut(int npcType)"));
    r.setRoom = findMethodBySignature(rooms, parseSignature("void SetRoom(int npcID, int x, int y)"));
    r.ok = r.activeWorld && r.path >= 0 && r.cloud >= 0 && r.npcs && r.shimmered && r.savesAndLoads &&
           r.townManager && r.active >= 0 && r.type >= 0 && r.townNPC >= 0 && r.position >= 0 &&
           r.homeless >= 0 && r.homeX >= 0 && r.homeY >= 0 && r.variation >= 0 && r.givenName >= 0 &&
           r.setDefaults && r.setGivenName && r.hasRoom && r.kickOut && r.setRoom;
    return r.ok;
}

template <typename T>
T& at(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<uint8_t*>(obj) + offset);
}

template <typename T>
T readStatic(FieldInfo* f) {
    T v{};
    il2cpp::api().field_static_get_value(f, &v);
    return v;
}

std::string toUtf8(Il2CppString* s) {
    std::string out;
    if (!s) return out;
    for (int32_t i = 0; i < s->length; ++i) {
        const char32_t c = s->chars[i];
        if (c == '\t' || c == '\n' || c == '\r') { out += ' '; continue; }
        if (c < 0x80) out += static_cast<char>(c);
        else if (c < 0x800) { out += static_cast<char>(0xC0 | (c >> 6)); out += static_cast<char>(0x80 | (c & 0x3F)); }
        else {
            out += static_cast<char>(0xE0 | (c >> 12));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        }
    }
    return out;
}

std::string activeWorldPath() {
    Il2CppObject* data = readStatic<Il2CppObject*>(g_refs.activeWorld);
    if (!data || at<uint8_t>(data, g_refs.cloud)) return {};
    return toUtf8(at<Il2CppString*>(data, g_refs.path));
}

// "<uid>/<nome>" <-> tipo, pelos NPCs registrados.
std::string keyOf(int type) {
    for (const ModNpcInfo& n : modNpcs()) {
        if (n.type == type) return n.mod + "/" + n.name;
    }
    return {};
}

int typeOf(const std::string& key) {
    const size_t slash = key.find('/');
    if (slash == std::string::npos) return -1;
    return modNpcTypeByName(key.substr(0, slash), key.substr(slash + 1));
}

std::vector<std::string> split(const std::string& s) {
    std::vector<std::string> cols;
    size_t start = 0;
    for (size_t tab; (tab = s.find('\t', start)) != std::string::npos; start = tab + 1) {
        cols.push_back(s.substr(start, tab - start));
    }
    cols.push_back(s.substr(start));
    return cols;
}

std::vector<std::string> readLines(const std::string& path) {
    std::vector<std::string> out;
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return out;
    char line[2048];
    bool header = false;
    while (std::fgets(line, sizeof(line), f)) {
        std::string s(line);
        while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
        if (!header) {
            header = true;
            if (s != kHeader) {
                BL_ERROR("moradores de mod: %s com cabecalho desconhecido; ignorado", path.c_str());
                break;
            }
            continue;
        }
        if (!s.empty()) out.push_back(s);
    }
    std::fclose(f);
    return out;
}

/** Temporario + rename: queda no meio nao deixa arquivo pela metade. */
void writeLines(const std::string& path, const std::vector<std::string>& lines) {
    if (lines.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("moradores de mod: nao consegui escrever %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kHeader);
    for (const std::string& l : lines) std::fprintf(f, "%s\n", l.c_str());
    const bool ok = std::fflush(f) == 0;
    std::fclose(f);
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("moradores de mod: falha ao gravar %s", path.c_str());
        std::remove(tmp.c_str());
    }
}

// O que o save de agora tirou do .wld, para o arquivo ao lado (NPCs e
// shimmer no SaveNPCs; salas no Save do TownRoomManager, que vem depois).
std::vector<std::string> g_pending;

// ------------------------------ salvar ------------------------------

using SaveNpcsFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
SaveNpcsFn g_origSaveNpcs = nullptr;

int32_t hkSaveNpcs(Il2CppObject* writer, const MethodInfo* m) {
    const Refs& r = g_refs;
    std::vector<std::string> lines;
    // O .wld grava quem tem townNPC (e os NPCID.Sets.SavesAndLoads): esses
    // flags saem so durante a chamada. NAO o `active` — o save automatico roda
    // noutra thread, com o jogo andando, e um NPC inativo teria o lugar
    // reusado nesse meio-tempo.
    std::vector<Il2CppObject*> hidden;
    std::vector<int> hiddenSets, hiddenShimmer;
    Il2CppArray* npcs = readStatic<Il2CppArray*>(r.npcs);
    Il2CppArray* sets = readStatic<Il2CppArray*>(r.savesAndLoads);
    Il2CppArray* shimmer = readStatic<Il2CppArray*>(r.shimmered);
    for (uintptr_t i = 0; npcs && i < npcs->length; ++i) {
        Il2CppObject* n = static_cast<Il2CppObject**>(arrayData(npcs))[i];
        if (!n || !at<uint8_t>(n, r.active)) continue;
        const int type = at<int32_t>(n, r.type);
        if (!isModNpc(type)) continue;
        const bool saves = sets && static_cast<uintptr_t>(type) < sets->length &&
                           static_cast<uint8_t*>(arrayData(sets))[type];
        if (!at<uint8_t>(n, r.townNPC) && !saves) continue;
        const std::string key = keyOf(type);
        if (key.empty()) continue;
        char buf[128];
        std::snprintf(buf, sizeof(buf), "%.2f\t%.2f\t%d\t%d\t%d\t%d", at<float>(n, r.position),
                      at<float>(n, r.position + 4), at<uint8_t>(n, r.homeless) ? 1 : 0, at<int32_t>(n, r.homeX),
                      at<int32_t>(n, r.homeY), at<int32_t>(n, r.variation));
        lines.push_back("npc\t" + key + "\t" + buf + "\t" + toUtf8(at<Il2CppString*>(n, r.givenName)));
        if (at<uint8_t>(n, r.townNPC)) {
            at<uint8_t>(n, r.townNPC) = 0;
            hidden.push_back(n);
        }
    }
    for (int type = kVanillaNpcCount; sets && type < static_cast<int>(sets->length); ++type) {
        auto* flags = static_cast<uint8_t*>(arrayData(sets));
        if (isModNpc(type) && flags[type]) {
            flags[type] = 0;
            hiddenSets.push_back(type);
        }
    }
    for (int type = kVanillaNpcCount; shimmer && type < static_cast<int>(shimmer->length); ++type) {
        auto* flags = static_cast<uint8_t*>(arrayData(shimmer));
        if (!flags[type]) continue;
        flags[type] = 0;
        hiddenShimmer.push_back(type);
        const std::string key = keyOf(type);
        if (!key.empty()) lines.push_back("shimmer\t" + key);
    }

    const int32_t result = g_origSaveNpcs(writer, m);

    for (Il2CppObject* n : hidden) at<uint8_t>(n, r.townNPC) = 1;
    for (int type : hiddenSets) static_cast<uint8_t*>(arrayData(sets))[type] = 1;
    for (int type : hiddenShimmer) static_cast<uint8_t*>(arrayData(shimmer))[type] = 1;
    {
        std::lock_guard<std::mutex> l(g_mx);
        g_pending = std::move(lines);
    }
    return result;
}

using RoomsSaveFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
RoomsSaveFn g_origRoomsSave = nullptr;

void hkRoomsSave(Il2CppObject* self, Il2CppObject* writer, const MethodInfo* m) {
    const Refs& r = g_refs;
    // A sala de cada morador de mod: fora do .wld durante a gravacao (o load
    // do jogo faz _hasRoom[tipo] = true, e sem o mod o tipo cai fora).
    std::vector<std::pair<int, Point>> rooms;
    for (const ModNpcInfo& n : modNpcs()) {
        Point p{};
        int32_t type = n.type;
        auto hasRoom = reinterpret_cast<bool (*)(Il2CppObject*, int32_t, Point*, const MethodInfo*)>(
            *reinterpret_cast<void* const*>(r.hasRoom));
        if (!hasRoom(self, type, &p, r.hasRoom)) continue;
        rooms.push_back({type, p});
        reinterpret_cast<void (*)(Il2CppObject*, int32_t, const MethodInfo*)>(
            *reinterpret_cast<void* const*>(r.kickOut))(self, type, r.kickOut);
    }
    g_origRoomsSave(self, writer, m);
    auto setRoom = reinterpret_cast<void (*)(Il2CppObject*, int32_t, int32_t, int32_t, const MethodInfo*)>(
        *reinterpret_cast<void* const*>(r.setRoom));
    for (const auto& [type, p] : rooms) setRoom(self, type, p.x, p.y, r.setRoom);

    // O Save do TownRoomManager vem depois do SaveNPCs: aqui o arquivo fecha.
    const std::string path = activeWorldPath();
    if (path.empty()) return;
    std::vector<std::string> lines;
    {
        std::lock_guard<std::mutex> l(g_mx);
        lines = std::move(g_pending);
        g_pending.clear();
        for (const auto& [type, p] : rooms) {
            const std::string key = keyOf(type);
            if (!key.empty()) lines.push_back("room\t" + key + "\t" + std::to_string(p.x) + "\t" + std::to_string(p.y));
        }
        auto it = g_kept.find(path);
        if (it != g_kept.end()) lines.insert(lines.end(), it->second.begin(), it->second.end());
    }
    writeLines(path + kSuffix, lines);
    if (!lines.empty()) {
        BL_INFO("moradores de mod: mundo salvo sem morador de mod no .wld; %zu linha(s) em %s",
                lines.size(), (path + kSuffix).c_str());
    }
}

// ------------------------------ carregar ------------------------------

using LoadNpcsFn = void (*)(Il2CppObject*, const MethodInfo*);
LoadNpcsFn g_origLoadNpcs = nullptr;

/** Um lugar livre em Main.npc (o ultimo e o vazio do jogo). */
Il2CppObject* freeNpc() {
    const Refs& r = g_refs;
    Il2CppArray* npcs = readStatic<Il2CppArray*>(r.npcs);
    for (uintptr_t i = 0; npcs && i + 1 < npcs->length; ++i) {
        Il2CppObject* n = static_cast<Il2CppObject**>(arrayData(npcs))[i];
        if (n && !at<uint8_t>(n, r.active)) return n;
    }
    return nullptr;
}

void restoreNpc(const SavedNpc& s, int type) {
    const Refs& r = g_refs;
    auto& a = il2cpp::api();
    Il2CppObject* n = freeNpc();
    if (!n) {
        BL_ERROR("moradores de mod: sem lugar livre em Main.npc para %s", s.key.c_str());
        return;
    }
    int32_t t = type;
    alignas(8) uint8_t spawnParams[32] = {};   // NPCSpawnParams sem nada
    void* args[2] = {&t, spawnParams};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(r.setDefaults, n, args, &exc);   // passa pelo SetDefaults do mod
    if (exc) {
        BL_ERROR("moradores de mod: SetDefaults de %s lancou excecao", s.key.c_str());
        return;
    }
    at<float>(n, r.position) = s.x;
    at<float>(n, r.position + 4) = s.y;
    at<uint8_t>(n, r.homeless) = s.homeless ? 1 : 0;
    at<int32_t>(n, r.homeX) = s.homeX;
    at<int32_t>(n, r.homeY) = s.homeY;
    at<int32_t>(n, r.variation) = s.variation;
    at<uint8_t>(n, r.active) = 1;
    Il2CppString* name = a.string_new(s.name.c_str());
    void* nameArg[1] = {name};
    a.runtime_invoke(r.setGivenName, n, nameArg, &exc);
}

void hkLoadNpcs(Il2CppObject* reader, const MethodInfo* m) {
    g_origLoadNpcs(reader, m);
    const std::string path = activeWorldPath();
    if (path.empty()) return;
    std::vector<std::string> kept;
    int restored = 0;
    Il2CppArray* shimmer = readStatic<Il2CppArray*>(g_refs.shimmered);
    for (const std::string& line : readLines(path + kSuffix)) {
        const std::vector<std::string> c = split(line);
        if (c.size() < 2) continue;
        const int type = typeOf(c[1]);
        if (type < 0) {   // o mod nao esta: fica para quando voltar
            kept.push_back(line);
            continue;
        }
        if (c[0] == "npc" && c.size() >= 9) {
            SavedNpc s;
            s.key = c[1];
            s.x = std::strtof(c[2].c_str(), nullptr);
            s.y = std::strtof(c[3].c_str(), nullptr);
            s.homeless = std::atoi(c[4].c_str());
            s.homeX = std::atoi(c[5].c_str());
            s.homeY = std::atoi(c[6].c_str());
            s.variation = std::atoi(c[7].c_str());
            s.name = c[8];
            restoreNpc(s, type);
            ++restored;
        } else if (c[0] == "shimmer" && shimmer && static_cast<uintptr_t>(type) < shimmer->length) {
            static_cast<uint8_t*>(arrayData(shimmer))[type] = 1;
        } else if (c[0] == "room") {
            kept.push_back(line);   // a sala volta no Load do TownRoomManager, que vem depois
        }
    }
    std::lock_guard<std::mutex> l(g_mx);
    g_kept[path] = kept;
    if (restored > 0) BL_INFO("moradores de mod: %d reposto(s) do arquivo ao lado", restored);
}

using RoomsLoadFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
RoomsLoadFn g_origRoomsLoad = nullptr;

void hkRoomsLoad(Il2CppObject* self, Il2CppObject* reader, const MethodInfo* m) {
    g_origRoomsLoad(self, reader, m);
    const Refs& r = g_refs;
    const std::string path = activeWorldPath();
    if (path.empty()) return;
    auto setRoom = reinterpret_cast<void (*)(Il2CppObject*, int32_t, int32_t, int32_t, const MethodInfo*)>(
        *reinterpret_cast<void* const*>(r.setRoom));
    std::lock_guard<std::mutex> l(g_mx);
    auto it = g_kept.find(path);
    if (it == g_kept.end()) return;
    std::vector<std::string> still;
    for (const std::string& line : it->second) {
        const std::vector<std::string> c = split(line);
        const int type = c.size() >= 4 && c[0] == "room" ? typeOf(c[1]) : -1;
        if (type < 0) {
            still.push_back(line);
            continue;
        }
        setRoom(self, type, std::atoi(c[2].c_str()), std::atoi(c[3].c_str()), r.setRoom);
    }
    it->second = still;
}

} // namespace

void installModNpcSave() {
    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    Il2CppClass* rooms = il2cpp::findClass({"Terraria.GameContent", "TownRoomManager", {}});
    auto& a = il2cpp::api();
    const MethodInfo* save = worldFile ? a.class_get_method_from_name(worldFile, "SaveNPCs", 1) : nullptr;
    const MethodInfo* load = worldFile ? a.class_get_method_from_name(worldFile, "LoadNPCs", 1) : nullptr;
    const MethodInfo* roomsSave = rooms ? a.class_get_method_from_name(rooms, "Save", 1) : nullptr;
    const MethodInfo* roomsLoad = rooms ? a.class_get_method_from_name(rooms, "Load", 1) : nullptr;
    if (!resolve() || !save || !load || !roomsSave || !roomsLoad ||
        !hook::install(save, hkSaveNpcs, &g_origSaveNpcs) || !hook::install(load, hkLoadNpcs, &g_origLoadNpcs) ||
        !hook::install(roomsSave, hkRoomsSave, &g_origRoomsSave) ||
        !hook::install(roomsLoad, hkRoomsLoad, &g_origRoomsLoad)) {
        BL_ERROR("moradores de mod: sem o save do mundo; morador de mod iria para o .wld pelo numero");
        return;
    }
    BL_INFO("moradores de mod: save do mundo pronto (%s)", kSuffix);
}

} // namespace bl::runtime
