// Compila junto com TexturePacks.cpp. Simula apenas Unity/IL2CPP; exercita o
// roteamento, arquivos, prioridade e restauracao do carregador de producao.
#include "content/textures/TexturePacks.h"
#include "content/textures/TexturePackNames.h"
#include "content/common/ContentAssets.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include <cassert>
#include <cstdarg>
#include <cstdlib>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <map>
#include <string>
#include <unistd.h>
#include <thread>
#include "hook/HookManager.h"

struct FieldInfo { std::string name; Il2CppArray* array; };
struct Asset : Il2CppObject { int width = 16, height = 16, state = 2; std::string name; Il2CppObject* value = this; Il2CppObject* source = nullptr; };
namespace {
std::map<std::string, FieldInfo> fields;
std::map<uint32_t, Il2CppObject*> handles;
uint32_t nextHandle = 1;
int loads = 0, destroyed = 0, mismatches = 0;
bool settled = false;
std::vector<Il2CppObject*> namedAssets;
void (*loadHook)(Il2CppObject*, const MethodInfo*) = nullptr;
auto** data(FieldInfo& f) { return static_cast<Il2CppObject**>(arrayData(f.array)); }
void file(const std::filesystem::path& root, const std::string& pack, const std::string& name, const char* text) {
    auto path = root / pack / "content/Images" / name;
    std::filesystem::create_directories(path.parent_path());
    std::ofstream(path) << text;
}
}
namespace bl::il2cpp {
Api& api() {
    static Api a = [] {
        Api x;
        x.field_static_get_value = [](FieldInfo* f, void* out) { *static_cast<Il2CppArray**>(out) = f->array; };
        x.field_get_name = [](FieldInfo* f) { return f->name.c_str(); };
        x.gchandle_new = [](Il2CppObject* o, bool) { uint32_t h = nextHandle++; handles[h] = o; return h; };
        x.gchandle_free = [](uint32_t h) { assert(handles.erase(h) == 1); };
        x.gchandle_get_target = [](uint32_t h) { return handles.at(h); };
        x.class_get_method_from_name = [](Il2CppClass*, const char*, int) { return reinterpret_cast<const MethodInfo*>(1); };
        return x;
    }();
    return a;
}
Il2CppClass* findClass(const TypeRef&) { return reinterpret_cast<Il2CppClass*>(1); }
FieldInfo* findField(Il2CppClass*, std::string_view name) {
    auto it = fields.find(std::string(name));
    return it == fields.end() ? nullptr : &it->second;
}
}
namespace bl::hook {
bool install(const MethodInfo*, void* replacement, void** original) {
    loadHook = reinterpret_cast<void (*)(Il2CppObject*, const MethodInfo*)>(replacement);
    *original = reinterpret_cast<void*>(+[](Il2CppObject*, const MethodInfo*) {});
    return true;
}
}
namespace bl::runtime {
bool contentSettled() { return settled; }
namespace content {
Il2CppObject* loadTextureAsset(const std::string& path, const unsigned char*, size_t, const std::string&, int* w, int* h) {
    ++loads;
    std::ifstream in(path);
    if (!(in >> *w >> *h)) return nullptr;
    auto* a = new Asset;
    a->width = *w; a->height = *h;
    return a;
}
bool textureAssetSize(Il2CppObject* o, int* w, int* h) {
    auto* a = static_cast<Asset*>(static_cast<Asset*>(o)->value); *w = a->width; *h = a->height; return true;
}
Il2CppClass* textureAssetClass() { return reinterpret_cast<Il2CppClass*>(2); }
std::string textureAssetName(Il2CppObject* o) { return static_cast<Asset*>(o)->name; }
Il2CppObject* textureAssetValue(Il2CppObject* o) { return o ? static_cast<Asset*>(o)->value : nullptr; }
Il2CppObject* textureAssetSource(Il2CppObject* o) { return static_cast<Asset*>(o)->source; }
int textureAssetState(Il2CppObject* o) { return static_cast<Asset*>(o)->state; }
bool replaceTextureAssetValue(Il2CppObject* o, Il2CppObject* value, Il2CppObject* source, int state) {
    assert(value || state != 2); auto* a = static_cast<Asset*>(o); a->value = value; a->source = source; a->state = state; return true;
}
std::vector<Il2CppObject*> textureAssets() { return namedAssets; }
void destroyTextureAsset(Il2CppObject* o) { ++destroyed; delete static_cast<Asset*>(o); }
bool setTableElement(FieldInfo* f, int i, Il2CppObject* o) { data(*f)[i] = o; return true; }
}
}
namespace bl::log {
void write(int, const char* fmt, ...) {
    if (std::string(fmt).find("size mismatch") != std::string::npos) ++mismatches;
}
}
int main() {
    using bl::runtime::textures::targetFor;
    const std::map<std::string, std::string> names = {
        {"Item_1.png", "Item"}, {"NPC_1.png", "Npc"}, {"Projectile_1.png", "Projectile"},
        {"Tiles_1.png", "Tile"}, {"Wall_1.png", "Wall"}, {"Buff_1.png", "Buff"}, {"Gore_1.png", "Gore"},
        {"Armor_Head_1.png", "ArmorHead"}, {"Armor_Body_1.png", "ArmorBody"},
        {"Armor_Arm_1.png", "ArmorArm"}, {"Armor_Legs_1.png", "ArmorLeg"},
        {"Female_Body_1.png", "FemaleBody"}, {"Armor/Armor_1.png", "ArmorBodyComposite"}
    };
    for (const auto& [name, table] : names) {
        auto t = targetFor(name); assert(t.table == table && t.index == 1);
        auto* array = static_cast<Il2CppArray*>(std::calloc(1, sizeof(Il2CppArray) + 4 * sizeof(void*)));
        array->length = 4;
        auto& f = fields.emplace(table, FieldInfo{table, array}).first->second;
        for (int i = 0; i < 4; ++i) data(f)[i] = new Asset;
    }
    for (const auto& name : {"NPC_-1.png", "Item_.png", "Item_2147483648.png", "Item_1 (Alternative).png", "Misc/NPC_1.png", "UI/Bestiary/NPC_1.png", "Extra_1.png", "Item_1.jpg", "NPC_1.PNG", "Item_1.png/evil"}) {
        assert(targetFor(name).index == -1);
    }
    assert(targetFor("Item_2147483647.png").index == 2147483647);
    auto root = std::filesystem::temp_directory_path() / ("bunny-textures-" + std::to_string(getpid()));
    std::filesystem::create_directories(root);
    std::map<std::string, Il2CppObject*> originals;
    for (const auto& [name, table] : names) {
        originals[table] = data(fields.at(table))[1];
        file(root, "upper", name, "32 16");
        file(root, "lower", name, "48 16");
    }
    file(root, "upper", "Item_2.png", "corrupt");
    file(root, "lower", "Item_2.png", "16 16");
    file(root, "upper", "Item_99.png", "16 16");
    file(root, "upper", "Misc/NPC_3.png", "100 100");
    file(root, "upper", "Item_1 (Alternative).png", "100 100");
    file(root, "script", "NPC_3.png", "100 100");
    std::filesystem::create_symlink(root / "upper/content/Images/Item_1.png", root / "upper/content/Images/Item_3.png");
    auto* unchanged = data(fields.at("Npc"))[3];
    auto* restoredItem2 = data(fields.at("Item"))[2];
    bl::runtime::configureTexturePacks(root.string(), {{"upper", "@texture"}, {"script", "main.js"}, {"lower", "@texture"}});
    bl::runtime::tickTexturePacks(); assert(loads == 0);
    settled = true; bl::runtime::tickTexturePacks();
    for (const auto& [table, original] : originals) {
        assert(data(fields.at(table))[1] != original);
        assert(static_cast<Asset*>(data(fields.at(table))[1])->width == 32);
    }
    assert(loads == static_cast<int>(names.size()) + 2);
    assert(mismatches == static_cast<int>(names.size()));
    assert(data(fields.at("Npc"))[3] == unchanged);
    assert(static_cast<Asset*>(data(fields.at("Item"))[2])->width == 16);
    bl::runtime::tickTexturePacks(); assert(loads == static_cast<int>(names.size()) + 2);
    bl::runtime::configureTexturePacks(root.string(), {{"lower", "@texture"}, {"upper", "@texture"}});
    bl::runtime::tickTexturePacks();
    for (const auto& [table, original] : originals) assert(static_cast<Asset*>(data(fields.at(table))[1])->width == 48);
    bl::runtime::configureTexturePacks(root.string(), {}); bl::runtime::tickTexturePacks();
    for (const auto& [table, original] : originals) assert(data(fields.at(table))[1] == original);
    assert(data(fields.at("Item"))[2] == restoredItem2);
    assert(handles.empty()); assert(destroyed == 2 * (static_cast<int>(names.size()) + 1));
    Asset inventory, bestiary, npc, lazy, skipped, source;
    inventory.name = "Images/Inventory_Back"; inventory.source = &source;
    bestiary.name = "Images/UI/Bestiary/NPC_3";
    npc.name = "Images/NPC_3";
    lazy.name = "Images\\UI\\Minimap\\Default\\MinimapFrame";
    lazy.value = nullptr; lazy.state = 0;
    skipped.name = "Images/UI/Skipped";
    namedAssets = {&inventory, &bestiary, &bestiary, &npc};
    file(root, "named-upper", "Inventory_Back.PNG", "40 40");
    file(root, "named-upper", "UI/Bestiary/NPC_3.png", "corrupt");
    file(root, "named-lower", "UI/Bestiary/NPC_3.png", "25 25");
    file(root, "named-upper", "UI/Minimap/Default/MinimapFrame.png", "200 200");
    file(root, "named-upper", "UI/Skipped.png", "30 30");
    std::filesystem::create_directory_symlink(root / "named-upper/content/Images/UI", root / "named-upper/content/Images/Linked");
    bl::runtime::configureTexturePacks(root.string(), {{"named-upper", "@texture"}, {"named-lower", "@texture"}});
    int before = loads;
    bl::runtime::tickTexturePacks();
    assert(loads == before + 3);
    assert(static_cast<Asset*>(inventory.value)->width == 40 && inventory.source == nullptr);
    assert(static_cast<Asset*>(bestiary.value)->width == 25 && npc.value == &npc);
    assert(lazy.value == nullptr && lazy.state == 0);
    std::thread worker([&] { loadHook(&skipped, nullptr); }); worker.join();
    assert(skipped.value == &skipped);
    loadHook(&lazy, nullptr);
    assert(static_cast<Asset*>(lazy.value)->width == 200 && loads == before + 4);
    loadHook(&lazy, nullptr); assert(loads == before + 4);
    bl::runtime::configureTexturePacks(root.string(), {}); bl::runtime::tickTexturePacks();
    assert(inventory.value == &inventory && inventory.source == &source);
    assert(bestiary.value == &bestiary && lazy.value == nullptr && lazy.state == 0 && handles.empty());
    namedAssets.clear();
    for (auto& [name, f] : fields) { for (int i = 0; i < 4; ++i) delete static_cast<Asset*>(data(f)[i]); std::free(f.array); }
    std::filesystem::remove_all(root);
    std::puts("texturepacks: routing, priority, corrupt fallback, bounds, isolation, reload, restore and GC handles: OK");
}
