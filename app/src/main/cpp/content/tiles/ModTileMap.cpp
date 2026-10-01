#include "content/tiles/ModTileMap.h"
#include "content/tiles/ModTiles.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace bl::runtime {

namespace {

constexpr const char* kSuffix = ".bl";   // <mapa>.map.bl
constexpr const char* kHeader = "bunny-map 1";
constexpr int kChunkShift = 6;           // WorldMapChunk.MapChunkSize = 64
constexpr int kChunkSize = 1 << kChunkShift;
constexpr int kChunkCells = kChunkSize * kChunkSize;
constexpr size_t kMaxChunks = 1 << 16;

/** Terraria.Map.MapTile: 4 bytes. */
struct MapCell {
    uint16_t type;
    uint8_t light;
    uint8_t extra;   // cor da tinta e bits de controle
};
static_assert(sizeof(MapCell) == 4, "layout do MapTile");

struct Entry {
    uint8_t r, g, b;
    std::string nameKey;
};

std::mutex g_mx;
std::vector<std::vector<Entry>> g_entries;   // indice = tipo - kVanillaTileCount
std::atomic<bool> g_dirty{false};
// Onde as entradas dos mods comecam; 0 = ainda nao aplicadas.
std::atomic<uint16_t> g_modPosition{0};
std::vector<int32_t> g_indexType;    // indice - modPosition -> tipo
std::vector<uint8_t> g_indexOption;  // indice - modPosition -> opcao
std::vector<int32_t> g_typeBase;     // tipo - kVanillaTileCount -> primeiro indice (0 = sem entrada)
// Os arrays que ja sao nossos: outro no lugar = o jogo refez. So comparados.
Il2CppArray* g_colorsOurs = nullptr;
Il2CppArray* g_legendOurs = nullptr;

// Os pedacos do mapa (64x64) que receberam indice de mod: so eles sao
// varridos na gravacao, sem descomprimir o mapa inteiro.
std::atomic<uint8_t> g_marks[kMaxChunks];
std::atomic<int> g_chunkWidth{0};
std::atomic<bool> g_scanAll{false};
std::atomic<bool> g_alwaysScanAll{false};   // sem o hook do CreateMapTile
// Sem o hook de gravar, o .map levaria indice de mod: tile de mod fica fora do mapa.
std::atomic<bool> g_disabled{false};

/** Celula de mod cujo mod nao esta carregado: volta ao arquivo enquanto o lugar seguir escuro. */
struct KeptCell {
    int x, y;
    MapCell cell;   // type = a opcao
    std::string key;
};
std::vector<KeptCell> g_kept;   // do mundo aberto (sob g_mx)

struct Refs {
    bool tried = false, ok = false, saveOk = false;
    FieldInfo *lookup = nullptr, *options = nullptr, *colors = nullptr, *hell = nullptr, *legend = nullptr;
    // O _mapLegendCache e um MapLegend (o LocalizedText[] fica dentro dele),
    // e nao o array: -1 aqui = o campo ja e o array.
    int32_t legendArray = -1, legendLength = -1;
    const MethodInfo* getText = nullptr;       // Language.GetText(string)
    const MethodInfo* createMapTile = nullptr;
    const MethodInfo* saveCompressed = nullptr;
    const MethodInfo* saveMap = nullptr;        // MapHelper.SaveMap(bool)
    const MethodInfo* mapLoad = nullptr;        // WorldMap.Load
    const MethodInfo* getChunkTile = nullptr;   // WorldMap.GetChunkTile(int, int, out WorldMapChunk)
    const MethodInfo* tryGetMapPath = nullptr;
    const MethodInfo* getMap = nullptr;         // Main.get_Map
    const MethodInfo* getPlayerData = nullptr;  // Main.get_ActivePlayerFileData
    const MethodInfo* chunkSave = nullptr;      // WorldMapChunk.SaveCompressed()
    FieldInfo* worldData = nullptr;             // Main.ActiveWorldFileData
    int32_t maxWidth = -1, maxHeight = -1, chunkWidth = -1, lockObject = -1;
    int32_t tileData = -1, dirty = -1, chunkX = -1, chunkY = -1;   // WorldMapChunk (X, Y: o pedaco, nao o tile)
};

bool isArrayType(const Il2CppType* t) {
    auto& a = il2cpp::api();
    char* name = t ? a.type_get_name(t) : nullptr;
    const size_t n = name ? std::strlen(name) : 0;
    const bool out = n > 2 && std::strcmp(name + n - 2, "[]") == 0;
    if (name) a.il2cpp_free(name);
    return out;
}

/**
 * Onde ficam os nomes do mapa. O Lang._mapLegendCache do jogo e um MapLegend,
 * com o LocalizedText[] num campo dele (e um Length, se houver): ler o objeto
 * como array escreveria alem do fim dele.
 */
bool resolveLegend(Refs& r) {
    auto& a = il2cpp::api();
    const Il2CppType* type = a.field_get_type(r.legend);
    if (isArrayType(type)) return true;
    Il2CppClass* cls = type ? a.class_from_il2cpp_type(type) : nullptr;
    void* it = nullptr;
    while (FieldInfo* f = cls ? a.class_get_fields(cls, &it) : nullptr) {
        if (a.field_get_flags(f) & 0x10) continue;   // so de instancia
        const auto offset = static_cast<int64_t>(a.field_get_offset(f));
        if (offset < static_cast<int64_t>(sizeof(Il2CppObject))) continue;
        const char* name = a.field_get_name(f);
        if (r.legendArray < 0 && isArrayType(a.field_get_type(f))) {
            r.legendArray = static_cast<int32_t>(offset);
        } else if (name && (!std::strcmp(name, "Length") || !std::strcmp(name, "_length"))) {
            r.legendLength = static_cast<int32_t>(offset);
        }
    }
    if (r.legendArray < 0) {
        BL_ERROR("tiles de mod: _mapLegendCache (%s) sem array de nomes; tile de mod fora do mapa",
                 cls ? a.class_get_name(cls) : "?");
        return false;
    }
    BL_DEBUG("tiles de mod: nomes do mapa em %s+%d (Length em %d)", a.class_get_name(cls), r.legendArray,
             r.legendLength);
    return true;
}

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* helper = findClass({"Terraria.Map", "MapHelper", {}});
    Il2CppClass* lang = findClass({"Terraria", "Lang", {}});
    Il2CppClass* language = findClass({"Terraria.Localization", "Language", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* map = findClass({"Terraria.Map", "WorldMap", {}});
    Il2CppClass* chunk = findClass({"Terraria.Map", "WorldMapChunk", {}});
    r.lookup = helper ? findField(helper, "tileLookup") : nullptr;
    r.options = helper ? findField(helper, "tileOptionCounts") : nullptr;
    r.colors = helper ? findField(helper, "colorLookup") : nullptr;
    r.hell = helper ? findField(helper, "hellPosition") : nullptr;
    r.legend = lang ? findField(lang, "_mapLegendCache") : nullptr;
    r.getText = language ? a.class_get_method_from_name(language, "GetText", 1) : nullptr;
    if (r.legend && !resolveLegend(r)) r.legend = nullptr;
    r.ok = r.lookup && r.options && r.colors && r.hell && r.legend && r.getText;
    if (!r.ok) {
        BL_ERROR("tiles de mod: mapa sem as tabelas (tileLookup=%p colorLookup=%p hellPosition=%p "
                 "_mapLegendCache=%p Language.GetText=%p); tile de mod fora do mapa",
                 (void*)r.lookup, (void*)r.colors, (void*)r.hell, (void*)r.legend, (void*)r.getText);
        return r;
    }
    r.createMapTile = a.class_get_method_from_name(helper, "CreateMapTile", 3);
    r.saveCompressed = a.class_get_method_from_name(helper, "InternalSaveMapCompressed", 0);
    r.saveMap = a.class_get_method_from_name(helper, "SaveMap", 1);
    r.mapLoad = map ? a.class_get_method_from_name(map, "Load", 0) : nullptr;
    r.getChunkTile = map ? a.class_get_method_from_name(map, "GetChunkTile", 3) : nullptr;
    r.tryGetMapPath = map ? a.class_get_method_from_name(map, "TryGetMapPath", 3) : nullptr;
    r.getMap = main ? a.class_get_method_from_name(main, "get_Map", 0) : nullptr;
    r.getPlayerData = main ? a.class_get_method_from_name(main, "get_ActivePlayerFileData", 0) : nullptr;
    r.worldData = main ? findField(main, "ActiveWorldFileData") : nullptr;
    r.maxWidth = map ? fieldOffset(map, "MaxWidth") : -1;
    r.maxHeight = map ? fieldOffset(map, "MaxHeight") : -1;
    r.chunkWidth = map ? fieldOffset(map, "MaxChunkWidth") : -1;
    r.lockObject = map ? fieldOffset(map, "LockObject") : -1;
    r.tileData = chunk ? fieldOffset(chunk, "TileData") : -1;
    r.dirty = chunk ? fieldOffset(chunk, "dirty") : -1;
    r.chunkX = chunk ? fieldOffset(chunk, "X") : -1;
    r.chunkY = chunk ? fieldOffset(chunk, "Y") : -1;
    r.chunkSave = chunk ? a.class_get_method_from_name(chunk, "SaveCompressed", 0) : nullptr;
    r.saveOk = r.createMapTile && r.saveCompressed && r.saveMap && r.mapLoad && r.getChunkTile && r.tryGetMapPath &&
               r.getMap && r.getPlayerData && r.worldData && r.maxWidth >= 0 && r.maxHeight >= 0 &&
               r.chunkWidth >= 0 && r.lockObject >= 0 && r.tileData >= 0 && r.dirty >= 0 &&
               r.chunkX >= 0 && r.chunkY >= 0 && r.chunkSave &&
               a.monitor_enter && a.monitor_exit;
    if (!r.saveOk) {
        BL_ERROR("tiles de mod: sem as refs de gravar o mapa (CreateMapTile=%p InternalSaveMapCompressed=%p "
                 "WorldMap.Load=%p GetChunkTile=%p TryGetMapPath=%p); tile de mod sai do mapa salvo",
                 (void*)r.createMapTile, (void*)r.saveCompressed, (void*)r.mapLoad, (void*)r.getChunkTile,
                 (void*)r.tryGetMapPath);
    }
    return r;
}

Il2CppArray* readStaticArray(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

template <typename R, typename... Args>
R call(const MethodInfo* m, Args... args) {
    return reinterpret_cast<R (*)(Args..., const MethodInfo*)>(il2cpp::methodPointer(m))(args..., m);
}

std::string toUtf8(Il2CppString* s) {
    std::string out;
    if (!s) return out;
    for (int32_t i = 0; i < s->length; ++i) {
        const char32_t c = s->chars[i];
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

// ---- entradas: cores e nomes ----

void writeColor(uint8_t* p, const Entry& e) {
    // O Color deste jogo fica na memoria como A, B, G, R (o desenho do mapa
    // escurece os bytes 1..3 pela luz e deixa o 0).
    p[0] = 255;
    p[1] = e.b;
    p[2] = e.g;
    p[3] = e.r;
}

/** O array de nomes do mapa (o de dentro do MapLegend), ou null antes do jogo montar. */
Il2CppArray* legendTexts(const Refs& r, Il2CppObject** holder) {
    Il2CppObject* obj = nullptr;
    il2cpp::api().field_static_get_value(r.legend, &obj);
    *holder = obj;
    if (!obj || r.legendArray < 0) return reinterpret_cast<Il2CppArray*>(obj);
    return field<Il2CppArray*>(obj, r.legendArray);
}

/** Os LocalizedText dos nomes no _mapLegendCache (aumentado se preciso). */
void applyLegend(const Refs& r, uint16_t pos, size_t count, bool force) {
    auto& a = il2cpp::api();
    Il2CppObject* holder = nullptr;
    Il2CppArray* legend = legendTexts(r, &holder);
    if (!legend) return;   // o jogo monta no MapHelper.Initialize; o hook dele reaplica
    const uintptr_t want = static_cast<uintptr_t>(pos) + count;
    if (legend == g_legendOurs && legend->length == want && !force) return;
    Il2CppArray* out = legend->length == want ? legend : TypeTables::resizedCopy(legend, want);
    if (!out) {
        BL_ERROR("tiles de mod: nao deu para aumentar o _mapLegendCache; nome de tile de mod sem texto");
        return;
    }
    auto** slots = static_cast<Il2CppObject**>(arrayData(out));
    for (size_t k = 0; k < count; ++k) {
        const int type = g_indexType[k];
        const Entry& e = g_entries[static_cast<size_t>(type - kVanillaTileCount)][g_indexOption[k]];
        // Pela chave do dicionario: a troca de idioma troca o texto do mesmo LocalizedText.
        Il2CppObject* text = call<Il2CppObject*>(r.getText, a.string_new(e.nameKey.c_str()));
        if (text) a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(out), reinterpret_cast<void**>(&slots[pos + k]), text);
    }
    if (out != legend) {
        if (r.legendArray < 0) {
            a.field_static_set_value(r.legend, out);
        } else {
            a.gc_wbarrier_set_field(holder, reinterpret_cast<void**>(&field<Il2CppArray*>(holder, r.legendArray)), out);
            if (r.legendLength >= 0) field<int32_t>(holder, r.legendLength) = static_cast<int32_t>(want);
        }
    }
    g_legendOurs = out;
}

// ---- pedacos do mapa ----

void markChunk(int x, int y) {
    const int width = g_chunkWidth.load(std::memory_order_relaxed);
    const size_t c = static_cast<size_t>(y >> kChunkShift) * static_cast<size_t>(width) +
                     static_cast<size_t>(x >> kChunkShift);
    if (width <= 0 || x < 0 || y < 0 || c >= kMaxChunks) {
        g_scanAll.store(true, std::memory_order_relaxed);
        return;
    }
    if (!g_marks[c].load(std::memory_order_relaxed)) g_marks[c].store(1, std::memory_order_relaxed);
}

void clearMarks() {
    for (auto& m : g_marks) m.store(0, std::memory_order_relaxed);
    g_scanAll.store(g_alwaysScanAll.load(std::memory_order_relaxed), std::memory_order_relaxed);
}

/** A celula (x, y), carregando o pedaco dela. `chunk` recebe o pedaco. */
MapCell* cellAt(const Refs& r, Il2CppObject* map, int x, int y, Il2CppObject** chunk) {
    *chunk = nullptr;
    if (x < 0 || y < 0 || x >= field<int32_t>(map, r.maxWidth) || y >= field<int32_t>(map, r.maxHeight)) return nullptr;
    return call<MapCell*>(r.getChunkTile, map, static_cast<int32_t>(x), static_cast<int32_t>(y), chunk);
}

void setDirty(const Refs& r, Il2CppObject* chunk) {
    if (chunk) field<uint8_t>(chunk, r.dirty) = 1;
}

/** O `.map` do jogador e mundo abertos (ele ja existe), ou "". */
std::string mapPath(const Refs& r) {
    Il2CppObject* player = call<Il2CppObject*>(r.getPlayerData);
    Il2CppObject* world = nullptr;
    il2cpp::api().field_static_get_value(r.worldData, &world);
    if (!player || !world) return {};
    Il2CppString* path = nullptr;
    const bool exists = call<bool>(r.tryGetMapPath, player, world, &path);
    return exists ? toUtf8(path) : std::string();
}

struct SavedCell {
    int x, y;
    MapCell cell;
};

/**
 * Antes de gravar: os pedacos marcados (ou todos) com celula de mod ficam
 * carregados e sujos, e o jogo os comprime de novo (o hkChunkSave tira as
 * celulas de mod na hora). Um pedaco comprimido antes (descarregado por falta
 * de uso) levaria o indice de mod no CompressedData para o arquivo.
 */
void prepareSave(const Refs& r, Il2CppObject* map, uint16_t pos) {
    const int width = field<int32_t>(map, r.chunkWidth);
    const int maxW = field<int32_t>(map, r.maxWidth), maxH = field<int32_t>(map, r.maxHeight);
    const int height = (maxH + kChunkSize - 1) >> kChunkShift;
    const bool all = g_scanAll.load(std::memory_order_relaxed);
    auto& a = il2cpp::api();
    int scanned = 0, withMod = 0;
    for (int cy = 0; cy < height; ++cy) {
        for (int cx = 0; cx < width; ++cx) {
            const size_t c = static_cast<size_t>(cy) * static_cast<size_t>(width) + static_cast<size_t>(cx);
            if (!all && (c >= kMaxChunks || !g_marks[c].load(std::memory_order_relaxed))) continue;
            const int x0 = cx << kChunkShift, y0 = cy << kChunkShift;
            if (x0 >= maxW) continue;
            Il2CppObject* chunk = nullptr;
            if (!cellAt(r, map, x0, y0, &chunk) || !chunk) continue;
            ++scanned;
            // A trava do pedaco, a mesma do jogo ao comprimir (sem outra por fora).
            a.monitor_enter(chunk);
            const auto* data = field<MapCell*>(chunk, r.tileData);
            bool any = false;
            for (int k = 0; data && k < kChunkCells && !any; ++k) any = data[k].type >= pos;
            if (any) setDirty(r, chunk);
            a.monitor_exit(chunk);
            if (any) ++withMod;
            else if (c < kMaxChunks) g_marks[c].store(0, std::memory_order_relaxed);
        }
    }
    if (all && width > 0 && !g_alwaysScanAll.load(std::memory_order_relaxed)) {
        g_scanAll.store(false, std::memory_order_relaxed);
    }
    BL_DEBUG("tiles de mod: mapa: %d pedaco(s) varrido(s), %d com celula de mod", scanned, withMod);
}

void writeFile(const std::string& path, const std::vector<SavedCell>& cells, const std::vector<KeptCell>& kept) {
    if (cells.empty() && kept.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("tiles de mod: nao deu para gravar %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kHeader);
    int written = 0;
    // x \t y \t luz \t extra \t opcao \t chave
    for (const SavedCell& s : cells) {
        const size_t k = static_cast<size_t>(s.cell.type - g_modPosition.load(std::memory_order_relaxed));
        if (k >= g_indexType.size()) continue;
        const std::string key = modTileKey(g_indexType[k]);
        if (key.empty()) continue;
        std::fprintf(f, "%d\t%d\t%u\t%u\t%u\t%s\n", s.x, s.y, s.cell.light, s.cell.extra, g_indexOption[k], key.c_str());
        ++written;
    }
    for (const KeptCell& c : kept) {
        std::fprintf(f, "%d\t%d\t%u\t%u\t%u\t%s\n", c.x, c.y, c.cell.light, c.cell.extra, c.cell.type, c.key.c_str());
        ++written;
    }
    const bool ok = std::fclose(f) == 0;
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("tiles de mod: nao deu para gravar %s", path.c_str());
        std::remove(tmp.c_str());
        return;
    }
    BL_DEBUG("tiles de mod: %s: %d celula(s) de mod (%zu de mod ausente)", path.c_str(), written, kept.size());
}

std::vector<KeptCell> readFile(const std::string& path) {
    std::vector<KeptCell> out;
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return out;
    char line[1024];
    bool header = false;
    while (std::fgets(line, sizeof(line), f)) {
        std::string s(line);
        while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
        if (!header) {
            header = true;
            if (s != kHeader) {
                BL_ERROR("tiles de mod: %s com cabecalho desconhecido; ignorado", path.c_str());
                break;
            }
            continue;
        }
        std::vector<std::string> cols;
        size_t start = 0;
        for (size_t tab; (tab = s.find('\t', start)) != std::string::npos; start = tab + 1) {
            cols.push_back(s.substr(start, tab - start));
        }
        cols.push_back(s.substr(start));
        if (cols.size() != 6 || cols[5].empty()) continue;
        KeptCell c;
        c.x = std::atoi(cols[0].c_str());
        c.y = std::atoi(cols[1].c_str());
        c.cell.light = static_cast<uint8_t>(std::atoi(cols[2].c_str()));
        c.cell.extra = static_cast<uint8_t>(std::atoi(cols[3].c_str()));
        c.cell.type = static_cast<uint16_t>(std::atoi(cols[4].c_str()));
        c.key = cols[5];
        out.push_back(std::move(c));
    }
    std::fclose(f);
    return out;
}

// ---- hooks ----

using CreateMapTileFn = uint32_t (*)(int32_t, int32_t, uint8_t, const MethodInfo*);
CreateMapTileFn g_origCreateMapTile = nullptr;

/** O MapTile volta em w0 (struct de 4 bytes): Type nos 16 de baixo. */
uint32_t hkCreateMapTile(int32_t i, int32_t j, uint8_t light, const MethodInfo* m) {
    const uint32_t tile = g_origCreateMapTile(i, j, light, m);
    const uint16_t pos = g_modPosition.load(std::memory_order_relaxed);
    if (pos && (tile & 0xFFFF) >= pos) markChunk(i, j);
    return tile;
}

// A gravacao em curso. Global, e nao local: se o jogo lancar no meio, o
// desenrolar passa por aqui sem destrutor (a libbunny nao tem excecoes), e o
// SaveMap, que pega a excecao, termina o servico.
//
// Sem trava nossa: o jogo comprime cada pedaco com a trava DELE
// (lock(Main.Map.Chunks[i]) no InternalSaveMapCompressed), e o hkChunkSave
// limpa, comprime e devolve as celulas de mod ali dentro. Antes o hook segurava
// o LockObject do mapa por fora do save inteiro: com a thread do jogo pegando
// as duas travas na ordem contraria, o jogo congelava ao entrar no mundo (o
// save do toon, logo depois de entrar, com o jogo mexendo no mapa).
struct SaveState {
    bool active = false;
    std::thread::id thread;         // a thread que grava
    uint16_t pos = 0;
    Il2CppObject* map = nullptr;
    std::vector<SavedCell> cells;   // as de mod que ficaram fora do .map (vao para o .map.bl)
};
SaveState g_save;   // so a thread que grava (o SaveMap segura o IOLock do jogo)

/** Grava o `.map.bl` e encerra a gravacao. */
void finishSave(const Refs& r, bool saved) {
    if (!g_save.active) return;
    g_save.active = false;
    std::vector<KeptCell> kept;
    {
        std::lock_guard<std::mutex> l(g_mx);
        // A celula de mod ausente segue no arquivo enquanto o lugar estiver escuro.
        for (const KeptCell& c : g_kept) {
            Il2CppObject* chunk = nullptr;
            const MapCell* p = cellAt(r, g_save.map, c.x, c.y, &chunk);
            if (p && p->type == 0 && p->light == 0) kept.push_back(c);
        }
    }
    if (!saved) {
        BL_ERROR("tiles de mod: a gravacao do mapa lancou; o .map.bl ficou como estava");
    } else if (const std::string path = mapPath(r); !path.empty()) {
        std::lock_guard<std::mutex> l(g_mx);
        writeFile(path + kSuffix, g_save.cells, kept);
    }
    BL_DEBUG("tiles de mod: mapa gravado, %zu celula(s) de mod fora do .map", g_save.cells.size());
    g_save.cells.clear();
    g_save.cells.shrink_to_fit();
}

using SaveFn = void (*)(const MethodInfo*);
SaveFn g_origSave = nullptr;

void hkSaveCompressed(const MethodInfo* m) {
    const Refs& r = refs();
    const uint16_t pos = g_modPosition.load(std::memory_order_relaxed);
    Il2CppObject* map = pos && !g_save.active ? call<Il2CppObject*>(r.getMap) : nullptr;
    if (!map) {
        g_origSave(m);
        return;
    }
    prepareSave(r, map, pos);
    g_save.active = true;
    g_save.thread = std::this_thread::get_id();
    g_save.pos = pos;
    g_save.map = map;
    g_save.cells.clear();
    g_origSave(m);
    finishSave(r, true);
}

/**
 * Um pedaco sendo comprimido para o arquivo (o jogo ja esta com a trava dele):
 * as celulas de mod viram escuro, o que o jogo grava como nao explorado, e
 * voltam logo depois. Fora da gravacao (o pedaco descarregado por falta de
 * uso), o jogo comprime como sempre.
 */
using ChunkSaveFn = void (*)(Il2CppObject*, const MethodInfo*);
ChunkSaveFn g_origChunkSave = nullptr;

void hkChunkSave(Il2CppObject* chunk, const MethodInfo* m) {
    if (!g_save.active || g_save.thread != std::this_thread::get_id() || !chunk) {
        g_origChunkSave(chunk, m);
        return;
    }
    const Refs& r = refs();
    auto* data = field<MapCell*>(chunk, r.tileData);
    if (!data) {
        g_origChunkSave(chunk, m);
        return;
    }
    const int x0 = field<int32_t>(chunk, r.chunkX) << kChunkShift;
    const int y0 = field<int32_t>(chunk, r.chunkY) << kChunkShift;
    const size_t first = g_save.cells.size();
    for (int k = 0; k < kChunkCells; ++k) {
        if (data[k].type < g_save.pos) continue;
        g_save.cells.push_back({x0 + (k & (kChunkSize - 1)), y0 + (k >> kChunkShift), data[k]});
        data[k] = MapCell{};
    }
    g_origChunkSave(chunk, m);
    if (g_save.cells.size() == first) return;
    for (size_t i = first; i < g_save.cells.size(); ++i) {
        const SavedCell& c = g_save.cells[i];
        data[(c.y - y0) * kChunkSize + (c.x - x0)] = c.cell;
    }
    // O CompressedData ficou sem as de mod: sujo, para o descarregar comprimir de novo com elas.
    setDirty(r, chunk);
}

using SaveMapFn = void (*)(bool, const MethodInfo*);
SaveMapFn g_origSaveMap = nullptr;

void hkSaveMap(bool forceSave, const MethodInfo* m) {
    g_origSaveMap(forceSave, m);
    finishSave(refs(), false);   // so faz algo se o InternalSaveMapCompressed lancou
}

using LoadFn = void (*)(Il2CppObject*, const MethodInfo*);
LoadFn g_origLoad = nullptr;

void hkMapLoad(Il2CppObject* self, const MethodInfo* m) {
    const Refs& r = refs();
    clearMarks();
    {
        std::lock_guard<std::mutex> l(g_mx);
        g_kept.clear();
    }
    if (self) g_chunkWidth.store(field<int32_t>(self, r.chunkWidth), std::memory_order_relaxed);
    g_origLoad(self, m);
    if (!self) return;
    g_chunkWidth.store(field<int32_t>(self, r.chunkWidth), std::memory_order_relaxed);
    const std::string path = mapPath(r);
    if (path.empty()) return;
    std::vector<KeptCell> cells = readFile(path + kSuffix);
    if (cells.empty()) return;

    const uint16_t pos = g_modPosition.load(std::memory_order_relaxed);
    Il2CppObject* lock = field<Il2CppObject*>(self, r.lockObject);
    if (lock) il2cpp::api().monitor_enter(lock);
    int placed = 0, skipped = 0;
    {
        std::lock_guard<std::mutex> l(g_mx);
        for (KeptCell& c : cells) {
            const int type = modTileTypeByKey(c.key);
            const size_t i = static_cast<size_t>(type - kVanillaTileCount);
            const bool known = pos && type >= kVanillaTileCount && i < g_typeBase.size() && g_typeBase[i] > 0;
            if (!known) {
                g_kept.push_back(std::move(c));
                continue;
            }
            const size_t count = g_entries[i].size();
            const int option = c.cell.type < count ? c.cell.type : 0;
            Il2CppObject* chunk = nullptr;
            MapCell* p = cellAt(r, self, c.x, c.y, &chunk);
            // Explorado sem o mod nesse meio tempo: vale o que o jogo viu.
            if (!p || p->type != 0 || p->light != 0) { ++skipped; continue; }
            *p = MapCell{static_cast<uint16_t>(g_typeBase[i] + option), c.cell.light, c.cell.extra};
            setDirty(r, chunk);
            markChunk(c.x, c.y);
            ++placed;
        }
    }
    if (lock) il2cpp::api().monitor_exit(lock);
    BL_DEBUG("tiles de mod: %s%s: %d celula(s) de mod no mapa, %d ja explorada(s) sem o mod, %zu de mod ausente",
             path.c_str(), kSuffix, placed, skipped, g_kept.size());
}

} // namespace

int addModTileMapEntry(int type, int r, int g, int b, const std::string& nameKey) {
    if (type < kVanillaTileCount || !isModTile(type) || g_disabled.load()) return -1;
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaTileCount);
    if (g_entries.size() <= i) g_entries.resize(i + 1);
    const auto byte = [](int v) { return static_cast<uint8_t>(v < 0 ? 0 : v > 255 ? 255 : v); };
    g_entries[i].push_back({byte(r), byte(g), byte(b), nameKey});
    g_dirty.store(true);
    return static_cast<int>(g_entries[i].size() - 1);
}

void applyModTileMap(int total, bool rebuilt) {
    const Refs& r = refs();
    if (!r.ok) return;
    if (rebuilt) g_dirty.store(true);
    Il2CppArray* lookup = readStaticArray(r.lookup);
    Il2CppArray* options = readStaticArray(r.options);
    Il2CppArray* colors = readStaticArray(r.colors);
    uint16_t hell = 0;
    il2cpp::api().field_static_get_value(r.hell, &hell);
    if (!lookup || !options || !colors || hell <= 1 || colors->length <= hell ||
        lookup->length < static_cast<uintptr_t>(total) || options->length < static_cast<uintptr_t>(total)) {
        static int waited = 0;
        if (++waited == 600) {
            BL_DEBUG("tiles de mod: mapa ainda sem tabelas (lookup %d, opcoes %d, cores %d, inferno em %d)",
                     lookup ? static_cast<int>(lookup->length) : -1, options ? static_cast<int>(options->length) : -1,
                     colors ? static_cast<int>(colors->length) : -1, hell);
        }
        return;
    }
    const bool fresh = colors != g_colorsOurs;
    Il2CppObject* holder = nullptr;
    if (!fresh && !g_dirty.load() && legendTexts(r, &holder) == g_legendOurs) return;
    g_dirty.store(false);

    std::lock_guard<std::mutex> l(g_mx);
    // Array do jogo: as nossas vem logo depois da cor do inferno, a ultima dele.
    const uint16_t pos = fresh ? static_cast<uint16_t>(hell + 1) : g_modPosition.load();
    if (fresh && colors->length != static_cast<uintptr_t>(hell) + 1) {
        BL_WARN("tiles de mod: colorLookup com %d cores e o inferno em %d; as de mod entram em %d",
                static_cast<int>(colors->length), hell, pos);
    }
    auto* lk = static_cast<uint16_t*>(arrayData(lookup));
    auto* op = static_cast<int32_t*>(arrayData(options));
    g_indexType.clear();
    g_indexOption.clear();
    g_typeBase.assign(static_cast<size_t>(total - kVanillaTileCount), 0);
    for (int type = kVanillaTileCount; type < total; ++type) {
        const size_t i = static_cast<size_t>(type - kVanillaTileCount);
        const size_t count = i < g_entries.size() ? g_entries[i].size() : 0;
        if (count == 0 || pos + g_indexType.size() + count > 0xFFFF) {
            lk[type] = 0;   // sem AddMapEntry: fora do mapa
            op[type] = 0;
            continue;
        }
        const int base = pos + static_cast<int>(g_indexType.size());
        lk[type] = static_cast<uint16_t>(base);
        op[type] = static_cast<int32_t>(count);
        g_typeBase[i] = base;
        for (size_t o = 0; o < count; ++o) {
            g_indexType.push_back(type);
            g_indexOption.push_back(static_cast<uint8_t>(o));
        }
    }
    const size_t count = g_indexType.size();
    const uintptr_t want = static_cast<uintptr_t>(pos) + count;
    Il2CppArray* out = colors->length == want ? colors : TypeTables::resizedCopy(colors, want);
    if (!out) {
        BL_ERROR("tiles de mod: nao deu para aumentar o colorLookup; tile de mod fora do mapa");
        for (int type = kVanillaTileCount; type < total; ++type) { lk[type] = 0; op[type] = 0; }
        g_typeBase.assign(g_typeBase.size(), 0);
        return;
    }
    auto* col = static_cast<uint8_t*>(arrayData(out));
    for (size_t k = 0; k < count; ++k) {
        const int type = g_indexType[k];
        writeColor(col + (pos + k) * 4, g_entries[static_cast<size_t>(type - kVanillaTileCount)][g_indexOption[k]]);
    }
    // Campo de referencia: o objeto, nao o endereco da variavel (que e da pilha).
    if (out != colors) il2cpp::api().field_static_set_value(r.colors, out);
    g_colorsOurs = out;
    applyLegend(r, pos, count, true);
    if (fresh) {
        // Conferencia da ordem dos bytes: a terra do jogo e (151, 107, 75).
        const uint8_t* dirt = col + static_cast<size_t>(lk[0]) * 4;
        BL_INFO("tiles de mod: mapa: %zu entrada(s) de mod a partir de %d (terra do jogo: %u %u %u %u)",
                count, pos, dirt[0], dirt[1], dirt[2], dirt[3]);
    }
    g_modPosition.store(pos);
}

bool readMapTile(int x, int y, int* type, int* light, int* paint) {
    const Refs& r = refs();
    if (!r.ok || !r.saveOk) return false;
    Il2CppObject* map = call<Il2CppObject*>(r.getMap);
    Il2CppObject* lock = map ? field<Il2CppObject*>(map, r.lockObject) : nullptr;
    if (!lock) return false;
    il2cpp::api().monitor_enter(lock);
    Il2CppObject* chunk = nullptr;
    const MapCell* p = cellAt(r, map, x, y, &chunk);
    const MapCell cell = p ? *p : MapCell{};
    il2cpp::api().monitor_exit(lock);
    if (!p) return false;
    *type = cell.type;
    *light = cell.light;
    *paint = cell.extra & 0x1F;   // MapTile.Color
    return true;
}

void installModTileMap() {
    const Refs& r = refs();
    if (!r.ok) return;
    if (!r.saveOk) {
        g_disabled.store(true);
        return;
    }
    if (!hook::install(r.createMapTile, hkCreateMapTile, &g_origCreateMapTile)) {
        BL_ERROR("tiles de mod: sem hook no MapHelper.CreateMapTile; o mapa salvo varre todos os pedacos");
        g_alwaysScanAll.store(true);
        g_scanAll.store(true);
    }
    if (!hook::install(r.mapLoad, hkMapLoad, &g_origLoad)) {
        BL_ERROR("tiles de mod: sem hook no WorldMap.Load; tile de mod some do mapa ao voltar ao mundo");
    }
    // Sem estes, o .map levaria indice de mod: melhor o tile ficar fora do mapa.
    if (!hook::install(r.chunkSave, hkChunkSave, &g_origChunkSave) ||
        !hook::install(r.saveMap, hkSaveMap, &g_origSaveMap) ||
        !hook::install(r.saveCompressed, hkSaveCompressed, &g_origSave)) {
        BL_ERROR("tiles de mod: sem hook no MapHelper.SaveMap/InternalSaveMapCompressed/WorldMapChunk.SaveCompressed; tile de mod fora do mapa");
        g_disabled.store(true);
        std::lock_guard<std::mutex> l(g_mx);
        g_entries.clear();
        return;
    }
}

} // namespace bl::runtime
