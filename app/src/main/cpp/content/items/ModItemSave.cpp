#include "content/items/ModItemSave.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/common/GameRefs.h"
#include "content/buffs/ModBuffs.h"
#include "content/items/ModItems.h"

#include <cstdio>
#include <cstdlib>
#include <map>
#include <mutex>
#include <string>
#include <vector>

namespace bl::runtime {

namespace {

constexpr const char* kHeader = "bunnyloader itens 1";
constexpr const char* kSuffix = ".bl";
constexpr const char* kBuffContainer = "buff";

/**
 * Um item de mod guardado: onde estava e o que era. Buff de mod vai no mesmo
 * arquivo, com container "buff": slot na lista, `stack` = tempo restante.
 */
struct SavedItem {
    std::string container;   // "inventory", "armor", "bank2", "loadout1.dye", "trash"...
    int slot = 0;
    int stack = 1;
    int prefix = 0;
    bool favorited = false;
    std::string key;         // "<uid>/<nome>"
};

struct Refs {
    bool ok = false;
    int32_t filePath = -1, fileCloud = -1, filePlayer = -1;   // FileData / PlayerFileData
    int32_t inventory = -1, armor = -1, dye = -1, miscEquips = -1, miscDyes = -1;
    int32_t trash = -1, loadouts = -1;
    int32_t bank[4] = {-1, -1, -1, -1};
    int32_t storageItems = -1;                  // InventoryStorage.item
    int32_t loadoutArmor = -1, loadoutDye = -1;  // EquipmentLoadout
    int32_t type = -1, stack = -1, prefix = -1, favorited = -1;   // Item
    const MethodInfo* setDefaults = nullptr;
    const MethodInfo* applyPrefix = nullptr;
};
Refs g_refs;

std::mutex g_mx;
// Itens de mod que nao deu para repor ao carregar (mod nao carregado agora),
// por caminho do personagem. Voltam para o arquivo no proximo save.
std::map<std::string, std::vector<SavedItem>> g_kept;

std::string toUtf8(Il2CppString* s) {
    std::string out;
    if (!s) return out;
    for (int i = 0; i < s->length; ++i) {
        uint32_t c = s->chars[i];
        if (c >= 0xD800 && c < 0xDC00 && i + 1 < s->length) {
            const uint32_t lo = s->chars[i + 1];
            if (lo >= 0xDC00 && lo < 0xE000) {
                c = 0x10000 + ((c - 0xD800) << 10) + (lo - 0xDC00);
                ++i;
            }
        }
        if (c < 0x80) {
            out += static_cast<char>(c);
        } else if (c < 0x800) {
            out += static_cast<char>(0xC0 | (c >> 6));
            out += static_cast<char>(0x80 | (c & 0x3F));
        } else if (c < 0x10000) {
            out += static_cast<char>(0xE0 | (c >> 12));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        } else {
            out += static_cast<char>(0xF0 | (c >> 18));
            out += static_cast<char>(0x80 | ((c >> 12) & 0x3F));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        }
    }
    return out;
}

Il2CppObject* objectAt(Il2CppObject* obj, int32_t offset) {
    return obj && offset >= 0 ? field<Il2CppObject*>(obj, offset) : nullptr;
}

/** Um lugar onde o jogador guarda itens: um array, ou um item so (a lixeira). */
struct Container {
    std::string name;
    Il2CppArray* items = nullptr;
    Il2CppObject* single = nullptr;

    int size() const { return items ? static_cast<int>(items->length) : (single ? 1 : 0); }
    Il2CppObject* at(int slot) const {
        if (slot < 0 || slot >= size()) return nullptr;
        return items ? static_cast<Il2CppObject**>(arrayData(items))[slot] : single;
    }
};

/** Tudo o que o save do personagem grava: inventario, equipamento, cofres, conjuntos. */
std::vector<Container> containersOf(Il2CppObject* player) {
    const Refs& r = g_refs;
    std::vector<Container> v;
    auto add = [&](const std::string& name, Il2CppObject* arr) {
        if (arr) v.push_back({name, reinterpret_cast<Il2CppArray*>(arr), nullptr});
    };
    add("inventory", objectAt(player, r.inventory));
    add("armor", objectAt(player, r.armor));
    add("dye", objectAt(player, r.dye));
    add("misc", objectAt(player, r.miscEquips));
    add("miscdye", objectAt(player, r.miscDyes));
    // Porquinho, cofre, forja defensora e cofre do vazio.
    const char* bankNames[4] = {"bank", "bank2", "bank3", "bank4"};
    for (int i = 0; i < 4; ++i) {
        add(bankNames[i], objectAt(objectAt(player, r.bank[i]), r.storageItems));
    }
    if (auto* loadouts = reinterpret_cast<Il2CppArray*>(objectAt(player, r.loadouts))) {
        for (uintptr_t i = 0; i < loadouts->length; ++i) {
            Il2CppObject* l = static_cast<Il2CppObject**>(arrayData(loadouts))[i];
            const std::string p = "loadout" + std::to_string(i);
            add(p + ".armor", objectAt(l, r.loadoutArmor));
            add(p + ".dye", objectAt(l, r.loadoutDye));
        }
    }
    if (Il2CppObject* t = objectAt(player, r.trash)) v.push_back({"trash", nullptr, t});
    return v;
}

// ------------------------------ arquivo ------------------------------

std::vector<SavedItem> readFile(const std::string& path) {
    std::vector<SavedItem> items;
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return items;
    char line[1024];
    bool first = true;
    while (std::fgets(line, sizeof line, f)) {
        std::string s(line);
        while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
        if (first) {
            first = false;
            if (s != kHeader) {
                BL_ERROR("save de itens de mod: cabecalho desconhecido em %s", path.c_str());
                break;
            }
            continue;
        }
        // container \t slot \t pilha \t prefixo \t favorito \t chave
        std::vector<std::string> cols;
        size_t start = 0;
        for (size_t tab; (tab = s.find('\t', start)) != std::string::npos; start = tab + 1) {
            cols.push_back(s.substr(start, tab - start));
        }
        cols.push_back(s.substr(start));
        if (cols.size() != 6 || cols[5].empty()) continue;
        SavedItem it;
        it.container = cols[0];
        it.slot = std::atoi(cols[1].c_str());
        it.stack = std::atoi(cols[2].c_str());
        it.prefix = std::atoi(cols[3].c_str());
        it.favorited = cols[4] == "1";
        it.key = cols[5];
        items.push_back(std::move(it));
    }
    std::fclose(f);
    return items;
}

/** Grava por arquivo temporario + rename: queda no meio nao deixa arquivo pela metade. */
void writeFile(const std::string& path, const std::vector<SavedItem>& items) {
    if (items.empty()) {
        std::remove(path.c_str());
        return;
    }
    const std::string tmp = path + ".tmp";
    FILE* f = std::fopen(tmp.c_str(), "wb");
    if (!f) {
        BL_ERROR("save de itens de mod: nao consegui escrever %s", tmp.c_str());
        return;
    }
    std::fprintf(f, "%s\n", kHeader);
    for (const SavedItem& it : items) {
        std::fprintf(f, "%s\t%d\t%d\t%d\t%d\t%s\n", it.container.c_str(), it.slot, it.stack,
                     it.prefix, it.favorited ? 1 : 0, it.key.c_str());
    }
    const bool ok = std::fflush(f) == 0;
    std::fclose(f);
    if (!ok || std::rename(tmp.c_str(), path.c_str()) != 0) {
        BL_ERROR("save de itens de mod: falha ao gravar %s", path.c_str());
        std::remove(tmp.c_str());
    }
}

/** Caminho do .plr, ou "" para personagem na nuvem (nao ha arquivo local ao lado). */
std::string localPath(Il2CppObject* fileData) {
    if (!fileData || field<uint8_t>(fileData, g_refs.fileCloud)) return {};
    return toUtf8(reinterpret_cast<Il2CppString*>(objectAt(fileData, g_refs.filePath)));
}

// ------------------------------ salvar ------------------------------

using SaveFn = void (*)(Il2CppObject*, const MethodInfo*);
SaveFn g_origSave = nullptr;

/**
 * DEPOIS do original: o que conta e o estado que o jogo acabou de gravar (ele
 * guarda o item do cursor no inventario antes de escrever).
 */
void hkInternalSavePlayerFile(Il2CppObject* fileData, const MethodInfo* m) {
    g_origSave(fileData, m);
    const std::string path = localPath(fileData);
    Il2CppObject* player = objectAt(fileData, g_refs.filePlayer);
    if (path.empty() || !player) return;

    const Refs& r = g_refs;
    std::vector<SavedItem> items;
    for (const Container& c : containersOf(player)) {
        for (int slot = 0; slot < c.size(); ++slot) {
            Il2CppObject* item = c.at(slot);
            if (!item) continue;
            const int type = field<int32_t>(item, r.type);
            const int stack = field<int32_t>(item, r.stack);
            if (!isModItem(type) || stack <= 0) continue;
            items.push_back({c.name, slot, stack, field<uint8_t>(item, r.prefix),
                             field<uint8_t>(item, r.favorited) != 0, modItemKey(type)});
        }
    }
    for (const SavedBuff& b : collectModBuffs(player)) {
        items.push_back({kBuffContainer, b.slot, b.time, 0, false, b.key});
    }
    size_t kept = 0;
    {
        std::lock_guard<std::mutex> l(g_mx);
        auto it = g_kept.find(path);
        if (it != g_kept.end()) {
            kept = it->second.size();
            items.insert(items.end(), it->second.begin(), it->second.end());
        }
    }
    writeFile(path + kSuffix, items);
    if (!items.empty()) {
        BL_INFO("save de itens de mod: %zu item(ns) gravado(s) (%zu de mod nao carregado)",
                items.size(), kept);
    }
}

// ------------------------------ carregar ------------------------------

/**
 * O tipo de `key` agora: o item do mod, se ele esta carregado; senao o "?"
 * que o representa. -1 se nem isso (o jogo ainda nao conhece os tipos de mod,
 * ou a reserva de "?" acabou): quem chama guarda a entrada no arquivo.
 */
int typeForKey(const std::string& key) {
    const int type = modItemTypeByKey(key);
    if (type >= 0) return type < itemTypeCount() ? type : -1;
    return unloadedTypeFor(key);
}

/**
 * Tipo de mod que NAO veio do nosso arquivo: numero cru do .plr/.wld, que so
 * vale se ainda for um tipo que o jogo conhece e que nao e um "?" sem dono.
 * O resto derrubava o jogo (o bau le ContentSamples[tipo] ao carregar o mundo).
 */
bool isStrayType(int type) {
    if (type < kVanillaItemCount) return false;
    if (itemTypeCount() <= kVanillaItemCount) return false;   // tipos de mod ainda nao instalados
    return type >= itemTypeCount() || (isUnloadedType(type) && modItemKey(type).empty());
}

/** Vazio para o jogo: sem tipo ou sem pilha. */
bool isEmpty(Il2CppObject* item) {
    return item && (field<int32_t>(item, g_refs.type) == 0 || field<int32_t>(item, g_refs.stack) <= 0);
}

void clearItem(Il2CppObject* item) {
    Il2CppObject* exc = nullptr;
    int t = 0;
    void* sd[2] = {&t, nullptr};
    il2cpp::api().runtime_invoke(g_refs.setDefaults, item, sd, &exc);
}

bool restore(Il2CppObject* item, int type, const SavedItem& saved) {
    auto& a = il2cpp::api();
    const Refs& r = g_refs;
    Il2CppObject* exc = nullptr;
    int t = type;
    void* sd[2] = {&t, nullptr};
    a.runtime_invoke(r.setDefaults, item, sd, &exc);   // passa pelo setDefaults do mod
    if (exc || field<int32_t>(item, r.type) != type) return false;
    field<int32_t>(item, r.stack) = saved.stack > 0 ? saved.stack : 1;
    if (saved.prefix > 0 && r.applyPrefix) {
        int p = saved.prefix;
        void* pa[1] = {&p};
        a.runtime_invoke(r.applyPrefix, item, pa, &exc);
    }
    field<uint8_t>(item, r.favorited) = saved.favorited ? 1 : 0;
    return true;
}

using LoadFn = Il2CppObject* (*)(Il2CppString*, bool, const MethodInfo*);
LoadFn g_origLoad = nullptr;

Il2CppObject* hkLoadPlayer(Il2CppString* playerPath, bool cloudSave, const MethodInfo* m) {
    Il2CppObject* fileData = g_origLoad(playerPath, cloudSave, m);
    const std::string path = localPath(fileData);
    Il2CppObject* player = objectAt(fileData, g_refs.filePlayer);
    if (path.empty() || !player) return fileData;

    std::vector<SavedItem> saved = readFile(path + kSuffix);
    std::vector<SavedItem> kept;
    // Os buffs primeiro, e fora da lista de itens (a chave nao e de item).
    std::vector<SavedBuff> buffs;
    for (auto it = saved.begin(); it != saved.end();) {
        if (it->container != kBuffContainer) { ++it; continue; }
        buffs.push_back({it->slot, it->stack, it->key});
        it = saved.erase(it);
    }
    const size_t buffCount = buffs.size();
    for (const SavedBuff& b : restoreModBuffs(player, buffs)) {
        kept.push_back({kBuffContainer, b.slot, b.time, 0, false, b.key});
    }
    const size_t buffsKept = kept.size();
    int restored = 0, moved = 0, unloaded = 0, stray = 0;
    if (!saved.empty()) {
        const std::vector<Container> containers = containersOf(player);
        const Container* inventory = nullptr;
        for (const Container& c : containers) if (c.name == "inventory") inventory = &c;

        for (const SavedItem& s : saved) {
            const int type = typeForKey(s.key);
            if (type < 0) { kept.push_back(s); continue; }
            if (isUnloadedType(type)) ++unloaded;
            Il2CppObject* slotItem = nullptr;
            for (const Container& c : containers) {
                if (c.name == s.container) slotItem = c.at(s.slot);
            }
            // O jogo NAO troca tipo desconhecido por nada em todo lugar: os
            // cofres (porquinho etc.) voltam com o numero cru do .plr. Se ja e
            // o tipo certo, nada a fazer; se e outro tipo de mod, o numero e que
            // esta velho (outros mods, outra ordem) e o lugar e nosso. Tratar
            // como ocupado punha uma copia no inventario.
            const int slotType = slotItem ? field<int32_t>(slotItem, g_refs.type) : 0;
            if (slotItem && slotType == type) { ++restored; continue; }
            Il2CppObject* target =
                isEmpty(slotItem) || slotType >= kVanillaItemCount ? slotItem : nullptr;
            if (!target && inventory) {
                // O lugar foi ocupado (o .plr foi mexido sem o loader): primeira
                // vaga do inventario principal, sem moedas, municao e cursor.
                for (int i = 0; i < 50 && !target; ++i) {
                    if (isEmpty(inventory->at(i))) target = inventory->at(i);
                }
                if (target) ++moved;
            }
            if (target && restore(target, type, s)) ++restored;
            else kept.push_back(s);
        }
    }
    // O que sobrou com numero cru invalido (cofre de antes deste arquivo, mod
    // que mudou) vira nada: o jogo nao sabe desenhar nem empilhar.
    for (const Container& c : containersOf(player)) {
        for (int slot = 0; slot < c.size(); ++slot) {
            Il2CppObject* item = c.at(slot);
            if (item && isStrayType(field<int32_t>(item, g_refs.type))) {
                clearItem(item);
                ++stray;
            }
        }
    }
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (kept.empty()) g_kept.erase(path);
        else g_kept[path] = kept;
    }
    if (buffCount) {
        BL_INFO("save de buffs de mod: %zu reposto(s), %zu so no arquivo", buffCount - buffsKept, buffsKept);
    }
    if (!saved.empty() || stray) {
        BL_INFO("save de itens de mod: personagem: %d reposto(s) (%d como \"?\")%s, %zu so no "
                "arquivo, %d numero(s) invalido(s) limpo(s)", restored, unloaded,
                moved ? ", algum fora do lugar" : "", kept.size(), stray);
    }
    return fileData;
}

// ------------------------------ mundo ------------------------------
//
// Os baus do mundo, com o mesmo arquivo ao lado (`<mundo>.wld.bl`). O bau e
// identificado pela coordenada, nao pelo indice: o jogo so grava os baus que
// existem e os renumera ao carregar.

struct ChestLayout {
    bool ok = false;
    FieldInfo* chests = nullptr;          // Main.chest
    FieldInfo* activeWorld = nullptr;     // Main.ActiveWorldFileData
    int32_t items = -1, x = -1, y = -1;   // Chest
    size_t stride = 0;                    // ChestItem no array
    size_t type = 0, stack = 0, prefix = 0, favorited = 0;   // dentro do ChestItem
};
ChestLayout g_chest;

/** Um ChestItem dentro do array do bau. */
struct ChestSlot {
    char* data = nullptr;

    int16_t& type() const { return *reinterpret_cast<int16_t*>(data + g_chest.type); }
    int16_t& stack() const { return *reinterpret_cast<int16_t*>(data + g_chest.stack); }
    uint8_t& prefix() const { return *reinterpret_cast<uint8_t*>(data + g_chest.prefix); }
    uint8_t& favorited() const { return *reinterpret_cast<uint8_t*>(data + g_chest.favorited); }
};

ChestSlot slotOf(Il2CppArray* items, int slot) {
    if (slot < 0 || static_cast<uintptr_t>(slot) >= items->length) return {};
    return {static_cast<char*>(arrayData(items)) + static_cast<size_t>(slot) * g_chest.stride};
}

std::string chestName(Il2CppObject* chest) {
    return "chest:" + std::to_string(field<int32_t>(chest, g_chest.x)) + "," +
           std::to_string(field<int32_t>(chest, g_chest.y));
}

/** Cada bau do mundo e o array de ChestItem dele. */
template <typename Fn>
void forEachChest(Fn fn) {
    Il2CppArray* chests = nullptr;
    il2cpp::api().field_static_get_value(g_chest.chests, &chests);
    if (!chests) return;
    for (uintptr_t i = 0; i < chests->length; ++i) {
        Il2CppObject* chest = static_cast<Il2CppObject**>(arrayData(chests))[i];
        auto* items = chest ? reinterpret_cast<Il2CppArray*>(objectAt(chest, g_chest.items)) : nullptr;
        if (items) fn(chest, items);
    }
}

std::string activeWorldPath() {
    Il2CppObject* data = nullptr;
    il2cpp::api().field_static_get_value(g_chest.activeWorld, &data);
    return localPath(data);
}

using SaveChestsFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
SaveChestsFn g_origSaveChests = nullptr;

/** ANTES do original: e exatamente o que ele vai escrever no .wld. */
int32_t hkSaveChests(Il2CppObject* writer, const MethodInfo* m) {
    const std::string path = activeWorldPath();
    if (!path.empty()) {
        std::vector<SavedItem> items;
        forEachChest([&](Il2CppObject* chest, Il2CppArray* chestItems) {
            for (int i = 0; i < static_cast<int>(chestItems->length); ++i) {
                const ChestSlot s = slotOf(chestItems, i);
                if (s.type() < kVanillaItemCount || s.stack() <= 0) continue;
                std::string key = modItemKey(s.type());
                if (key.empty()) continue;
                items.push_back({chestName(chest), i, s.stack(), s.prefix(), s.favorited() != 0,
                                 std::move(key)});
            }
        });
        size_t kept = 0;
        {
            std::lock_guard<std::mutex> l(g_mx);
            auto it = g_kept.find(path);
            if (it != g_kept.end()) {
                kept = it->second.size();
                items.insert(items.end(), it->second.begin(), it->second.end());
            }
        }
        writeFile(path + kSuffix, items);
        if (!items.empty()) {
            BL_INFO("save de itens de mod: mundo: %zu item(ns) em bau gravado(s) (%zu so no arquivo)",
                    items.size(), kept);
        }
    }
    return g_origSaveChests(writer, m);
}

using FixFn = void (*)(const MethodInfo*);
FixFn g_origFix = nullptr;

/**
 * ANTES do original: e ele que le ContentSamples[tipo] de cada item de bau, e
 * lancava KeyNotFound — o mundo nem abria — quando o mod do item nao estava.
 */
void hkFixAgainstExploits(const MethodInfo* m) {
    const std::string path = activeWorldPath();
    const std::vector<SavedItem> saved =
        path.empty() ? std::vector<SavedItem>{} : readFile(path + kSuffix);
    std::vector<SavedItem> kept;
    int restored = 0, unloaded = 0, stray = 0;

    std::map<std::string, Il2CppArray*> byName;
    forEachChest([&](Il2CppObject* chest, Il2CppArray* items) { byName[chestName(chest)] = items; });
    // Lugares que o arquivo explicou; o resto com tipo de mod e numero cru.
    std::map<std::string, std::vector<bool>> covered;

    for (const SavedItem& s : saved) {
        auto it = byName.find(s.container);
        const int type = it == byName.end() ? -1 : typeForKey(s.key);
        const ChestSlot slot = type < 0 ? ChestSlot{} : slotOf(it->second, s.slot);
        // Item do JOGO no lugar: o bau foi mexido sem o loader, e ele fica.
        const int current = slot.data ? slot.type() : -1;
        if (!slot.data || (current > 0 && current < kVanillaItemCount)) {
            kept.push_back(s);
            continue;
        }
        slot.type() = static_cast<int16_t>(type);
        slot.stack() = static_cast<int16_t>(s.stack > 0 ? s.stack : 1);
        slot.prefix() = static_cast<uint8_t>(s.prefix);
        slot.favorited() = s.favorited ? 1 : 0;
        std::vector<bool>& cov = covered[s.container];
        cov.resize(it->second->length);
        cov[static_cast<size_t>(s.slot)] = true;
        ++restored;
        if (isUnloadedType(type)) ++unloaded;
    }
    forEachChest([&](Il2CppObject* chest, Il2CppArray* items) {
        auto cov = covered.find(chestName(chest));
        for (int i = 0; i < static_cast<int>(items->length); ++i) {
            const bool explained = cov != covered.end() && cov->second[static_cast<size_t>(i)];
            const ChestSlot s = slotOf(items, i);
            if (!explained && isStrayType(s.type())) {
                s.type() = 0;
                s.stack() = 0;
                s.prefix() = 0;
                s.favorited() = 0;
                ++stray;
            }
        }
    });
    if (!path.empty()) {
        std::lock_guard<std::mutex> l(g_mx);
        if (kept.empty()) g_kept.erase(path);
        else g_kept[path] = kept;
    }
    if (!saved.empty() || stray) {
        BL_INFO("save de itens de mod: mundo: %d item(ns) em bau reposto(s) (%d como \"?\"), "
                "%zu so no arquivo, %d numero(s) invalido(s) limpo(s)", restored, unloaded,
                kept.size(), stray);
    }
    g_origFix(m);
}

bool resolveChests() {
    using namespace il2cpp;
    auto& a = api();
    ChestLayout& c = g_chest;
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* chest = findClass({"Terraria", "Chest", {}});
    Il2CppClass* chestItem = findClass({"Terraria", "ChestItem", {}});
    if (!main || !chest || !chestItem) return false;
    c.chests = findField(main, "chest");
    c.activeWorld = findField(main, "ActiveWorldFileData");
    c.items = fieldOffset(chest, "item");
    c.x = fieldOffset(chest, "x");
    c.y = fieldOffset(chest, "y");
    uint32_t align = 0;
    c.stride = static_cast<size_t>(a.class_value_size(chestItem, &align));
    // Campo de struct: o IL2CPP conta o cabecalho de objeto (a struct em caixa).
    const int32_t header = static_cast<int32_t>(sizeof(Il2CppObject));
    const int32_t t = fieldOffset(chestItem, "type"), s = fieldOffset(chestItem, "stack"),
                  p = fieldOffset(chestItem, "prefix"), f = fieldOffset(chestItem, "favorited");
    if (t < header || s < header || p < header || f < header) return false;
    c.type = static_cast<size_t>(t - header);
    c.stack = static_cast<size_t>(s - header);
    c.prefix = static_cast<size_t>(p - header);
    c.favorited = static_cast<size_t>(f - header);
    c.ok = c.chests && c.activeWorld && c.items >= 0 && c.x >= 0 && c.y >= 0 && c.stride > 0 &&
           c.type + 2 <= c.stride && c.stack + 2 <= c.stride && c.prefix < c.stride &&
           c.favorited < c.stride;
    BL_INFO("save de itens de mod: ChestItem de %zu bytes (tipo +%zu, pilha +%zu, prefixo +%zu, "
            "favorito +%zu)", c.stride, c.type, c.stack, c.prefix, c.favorited);
    return c.ok;
}

// ------------------------------ o "?" ------------------------------

using SetDefaultsFn = void (*)(Il2CppObject*, int32_t, Il2CppObject*, const MethodInfo*);
SetDefaultsFn g_origSetDefaults = nullptr;

/**
 * O "?" nao tem mod, entao nao passa pelo hook JS dos itens de mod; e o
 * SetDefaults do jogo zera tipo acima de ItemID.Count. Um desvio nativo so
 * para ele, que o resto repassa sem custo.
 */
void hkSetDefaults(Il2CppObject* self, int32_t type, Il2CppObject* variant, const MethodInfo* m) {
    if (isUnloadedType(type)) {
        setupUnloadedItem(self, type);
        return;
    }
    g_origSetDefaults(self, type, variant, m);
}


bool resolve() {
    using namespace il2cpp;
    Refs& r = g_refs;
    Il2CppClass* fileData = findClass({"Terraria.IO", "FileData", {}});
    Il2CppClass* playerFileData = findClass({"Terraria.IO", "PlayerFileData", {}});
    Il2CppClass* player = findClass({"Terraria", "Player", {}});
    Il2CppClass* item = findClass({"Terraria", "Item", {}});
    Il2CppClass* storage = findClass({"Terraria", "InventoryStorage", {}});
    Il2CppClass* loadout = findClass({"Terraria", "EquipmentLoadout", {}});
    if (!fileData || !playerFileData || !player || !item || !storage || !loadout) return false;

    r.filePath = fieldOffset(fileData, "_path");
    r.fileCloud = fieldOffset(fileData, "_isCloudSave");
    r.filePlayer = fieldOffset(playerFileData, "_player");
    r.inventory = fieldOffset(player, "inventory");
    r.armor = fieldOffset(player, "armor");
    r.dye = fieldOffset(player, "dye");
    r.miscEquips = fieldOffset(player, "miscEquips");
    r.miscDyes = fieldOffset(player, "miscDyes");
    r.trash = fieldOffset(player, "trashItem");
    r.loadouts = fieldOffset(player, "Loadouts");
    const char* banks[4] = {"bank", "bank2", "bank3", "bank4"};
    for (int i = 0; i < 4; ++i) r.bank[i] = fieldOffset(player, banks[i]);
    r.storageItems = fieldOffset(storage, "item");
    r.loadoutArmor = fieldOffset(loadout, "Armor");
    r.loadoutDye = fieldOffset(loadout, "Dye");
    r.type = fieldOffset(item, "type");
    r.stack = fieldOffset(item, "stack");
    r.prefix = fieldOffset(item, "prefix");
    r.favorited = fieldOffset(item, "favorited");
    r.setDefaults = findMethodBySignature(item, parseSignature("void SetDefaults(int Type, ItemVariant variant)"));
    r.applyPrefix = findMethodBySignature(item, parseSignature("bool Prefix(int prefixWeWant)"));

    // Conjuntos (Loadouts), cofres e prefixo sao opcionais: sem eles, so
    // aquele lugar fica de fora. O resto e o minimo para nao perder item.
    r.ok = r.filePath >= 0 && r.fileCloud >= 0 && r.filePlayer >= 0 && r.inventory >= 0 &&
           r.type >= 0 && r.stack >= 0 && r.prefix >= 0 && r.favorited >= 0 && r.setDefaults;
    return r.ok;
}

} // namespace

void installModItemSave() {
    if (!resolve()) {
        BL_ERROR("save de itens de mod: refs faltando; itens de mod somem ao sair do mundo");
        return;
    }
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    const MethodInfo* save = il2cpp::findMethodBySignature(
        player, il2cpp::parseSignature("void InternalSavePlayerFile(PlayerFileData playerFile)"));
    const MethodInfo* load = il2cpp::findMethodBySignature(
        player, il2cpp::parseSignature("PlayerFileData LoadPlayer(string playerPath, bool cloudSave)"));
    const bool okSave = save && hook::install(save, hkInternalSavePlayerFile, &g_origSave);
    const bool okLoad = load && hook::install(load, hkLoadPlayer, &g_origLoad);
    if (!okSave || !okLoad) {
        BL_ERROR("save de itens de mod: sem hook (save=%d load=%d); itens de mod somem ao sair "
                 "do mundo", okSave, okLoad);
        return;
    }

    if (!hook::install(g_refs.setDefaults, hkSetDefaults, &g_origSetDefaults)) {
        BL_ERROR("save de itens de mod: sem hook em Item.SetDefaults; item \"?\" nao funciona");
    }

    Il2CppClass* worldFile = il2cpp::findClass({"Terraria.IO", "WorldFile", {}});
    const MethodInfo* saveChests = worldFile ? il2cpp::findMethodBySignature(
        worldFile, il2cpp::parseSignature("int SaveChests(BinaryWriter writer)")) : nullptr;
    const MethodInfo* fix = worldFile ? il2cpp::api().class_get_method_from_name(
        worldFile, "FixAgainstExploits", 0) : nullptr;
    const bool okWorld = resolveChests() && saveChests && fix &&
                         hook::install(saveChests, hkSaveChests, &g_origSaveChests) &&
                         hook::install(fix, hkFixAgainstExploits, &g_origFix);
    if (!okWorld) {
        BL_ERROR("save de itens de mod: sem os baus do mundo; item de mod em bau fica pelo "
                 "numero, e sem o mod o mundo nao abre");
    }
    BL_INFO("save de itens de mod: pronto (personagem%s)", okWorld ? " e baus do mundo" : "");
}

} // namespace bl::runtime
