#include "content/items/ModPrefixes.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <map>
#include <mutex>
#include <vector>

namespace bl::runtime {

namespace {

// O jogo nao tem mais que isto: Item.prefix e ChestItem.prefix sao byte.
constexpr int kMaxPrefix = 255;
// O PrefixID.Count desta versao, se nao der para ler do jogo.
constexpr int kFallbackVanillaCount = 98;

std::mutex g_mx;
int g_vanilla = -1;
std::vector<std::string> g_keys;          // id - vanilla -> "<uid>/<nome>"
std::map<std::string, int> g_byKey;

/**
 * Lido UMA vez e guardado: o PrefixLoader sobe o PrefixID.Count depois, e a
 * conta dos numeros de mod tem de continuar partindo do de fabrica.
 */
int readVanillaCount() {
    auto& a = il2cpp::api();
    Il2CppClass* cls = il2cpp::findClass({"Terraria.ID", "PrefixID", {}});
    FieldInfo* f = cls ? il2cpp::findField(cls, "Count") : nullptr;
    int32_t count = 0;
    if (f && a.field_static_get_value) {
        if (a.runtime_class_init) a.runtime_class_init(cls);
        a.field_static_get_value(f, &count);
    }
    if (count <= 0 || count > kMaxPrefix) {
        BL_ERROR("prefixos de mod: PrefixID.Count ilegivel (%d); usando %d", count, kFallbackVanillaCount);
        return kFallbackVanillaCount;
    }
    return count;
}

int vanillaLocked() {
    if (g_vanilla < 0) g_vanilla = readVanillaCount();
    return g_vanilla;
}

} // namespace

int vanillaPrefixCount() {
    std::lock_guard<std::mutex> l(g_mx);
    return vanillaLocked();
}

int registerModPrefix(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    const std::string key = mod + "/" + name;
    if (g_byKey.count(key)) return -1;
    const int id = vanillaLocked() + static_cast<int>(g_keys.size());
    if (id > kMaxPrefix) {
        BL_ERROR("prefixos de mod: %s nao cabe (o jogo guarda o prefixo em 1 byte: ate %d)", key.c_str(), kMaxPrefix);
        return -1;
    }
    g_keys.push_back(key);
    g_byKey[key] = id;
    return id;
}

std::string modPrefixKey(int id) {
    std::lock_guard<std::mutex> l(g_mx);
    const int i = id - vanillaLocked();
    return i >= 0 && i < static_cast<int>(g_keys.size()) ? g_keys[static_cast<size_t>(i)] : std::string();
}

int modPrefixByKey(const std::string& key) {
    std::lock_guard<std::mutex> l(g_mx);
    auto it = g_byKey.find(key);
    return it == g_byKey.end() ? -1 : it->second;
}

} // namespace bl::runtime
