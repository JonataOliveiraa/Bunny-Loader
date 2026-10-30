#include "script/dotnet/NativeApi.h"
#include "script/dotnet/DotnetCall.h"
#include "boot/Boot.h"
#include "content/buffs/BuffIconLook.h"
#include "content/players/PlayerStages.h"
#include "content/buffs/ModBuffs.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModContent.h"
#include "content/common/ModLanguage.h"
#include "content/common/ModNet.h"
#include "content/common/TypeTables.h"
#include "content/items/ItemDrawStages.h"
#include "content/items/ModEquips.h"
#include "content/items/ModItemData.h"
#include "content/items/ModItemSave.h"
#include "content/items/ModPrefixes.h"
#include "content/items/ModRarities.h"
#include "content/items/ModItems.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "menu/ModMenu.h"
#include "mods/ModLoader.h"
#include "script/api/Console.h"

#include <dlfcn.h>
#include <sys/stat.h>
#include <unistd.h>

#include <algorithm>
#include <cstring>
#include <map>


namespace bl::dotnet {

namespace {

/**
 * O que o C# chama do nativo. A ORDEM e o layout sao contrato com
 * Interop/NativeApi.cs: campo novo entra no fim, e `size` deixa o C# recusar
 * uma tabela mais velha que ele.
 */
struct NativeApi {
    uint32_t size;
    void (*log)(int32_t level, const char* text);
    void* (*findClass)(const char* ns, const char* name);
    int32_t (*fieldOffset)(void* cls, const char* name);
    void* (*findMethod)(void* cls, const char* name, int32_t argc);
    void** (*installHook)(void* method, void* replacement);
    void* (*methodPointer)(void* method);
    void* (*newObject)(void* cls);
    void* (*findNestedClass)(void* outer, const char* name);
    void* (*findMethodBySignature)(void* cls, const char* signature);
    int32_t (*checkLayout)(void* method, const int32_t* sizes, int32_t count);
    int32_t (*callGame)(void* fn, CallFrame* frame);
    const char* (*lastCallError)();
    void* (*findField)(void* cls, const char* name);
    void* (*staticFieldAddress)(void* field);
    void (*staticFieldGet)(void* field, void* value);
    void (*staticFieldSet)(void* field, void* value);
    void* (*newString)(const uint16_t* chars, int32_t length);
    int32_t (*classValueSize)(void* cls);
    int32_t (*isInstance)(void* obj, void* cls);
    uint32_t (*gcHandleNew)(void* obj, int32_t pinned);
    void* (*gcHandleTarget)(uint32_t handle);
    void (*gcHandleFree)(uint32_t handle);
    void* (*newArray)(void* elementClass, int32_t length);
    const char* (*className)(void* cls);
    void* (*virtualMethod)(void* obj, void* method);
    int32_t (*registerItem)(const char* mod, const char* name, const char* texture,
                            const char* displayName, const char* tooltip);
    void (*prepareItem)(void* item, int32_t type);
    void (*finishItem)(void* item, int32_t type);
    void (*onItemsInstalled)(void (*callback)(int32_t first, int32_t last));
    void (*onContentReady)(void (*callback)());
    int32_t (*isGameThread)();
    void* (*requestModTexture)(const char* file, const char* assetName);
    void* (*requestVanillaTexture)(const char* name, int32_t* missingOnMobile);
    void* (*textureAssetValue)(void* asset);
    int32_t (*modManifestField)(const char* uid, int32_t field, char* out, int32_t size);
    void* (*languageText)(const char* key, const char* packedTexts, int32_t replace);
    uint32_t (*gcHandleNewWeak)(void* obj);
    void (*onItemData)(int32_t (*save)(void* item, const char* current, void* sink), int32_t (*load)(void* item, const char* data));
    void (*writeString)(void* sink, const char* text);
    int32_t (*netInstall)(void (*handler)(const uint8_t* data, int32_t length, int32_t from));
    int32_t (*netSend)(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient);
    int32_t (*netMode)();
    void (*netHello)(const char* hello, const char* refusal);
    int32_t (*itemDrawListen)(int32_t kind, int32_t (*pre)(void* args), void (*post)(), uint8_t* active);
    void (*consolePrint)(int32_t kind, const char* text);
    void* (*expandChestItem)(int32_t chest, int32_t slot);
    int32_t (*storeChestItem)(int32_t chest, int32_t slot, void* item);
    int32_t (*allocateRarity)();
    int32_t (*rarityCount)();
    int32_t (*registerPrefix)(const char* mod, const char* name);
    int32_t (*prefixCount)();
    int32_t (*growEquipSets)(const char* kind, int32_t from, int32_t to);
    void* (*resizeArray)(void* array, int32_t length);
    int32_t (*registerBuff)(const char* mod, const char* name, const char* texture, const char* displayName, const char* description);
    void (*onBuffsInstalled)(void (*callback)(int32_t first, int32_t last));
    void* (*buffIconLook)();
    int32_t (*onNaturalRegen)(float (*callback)(void* player, float regen));
    int32_t (*onConsumeBait)(int32_t (*callback)(void* player, void* bait));
    int32_t (*onDyeReward)(int32_t (*callback)(void* player, int32_t* pool, int32_t count, int32_t capacity));
};

// Exportados pela libil2cpp que a il2cpp::Api nao carrega.
struct Extra {
    Il2CppClass* (*classFromType)(const Il2CppType*) = nullptr;
    void* (*stringNewUtf16)(const uint16_t*, int32_t) = nullptr;
    const MethodInfo* (*objectGetVirtualMethod)(Il2CppObject*, const MethodInfo*) = nullptr;
};

const Extra& extra() {
    static Extra e = [] {
        Extra x;
        if (void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW)) {
            x.classFromType = reinterpret_cast<Il2CppClass* (*)(const Il2CppType*)>(dlsym(lib, "il2cpp_class_from_type"));
            x.stringNewUtf16 = reinterpret_cast<void* (*)(const uint16_t*, int32_t)>(dlsym(lib, "il2cpp_string_new_utf16"));
            x.objectGetVirtualMethod = reinterpret_cast<const MethodInfo* (*)(Il2CppObject*, const MethodInfo*)>(
                dlsym(lib, "il2cpp_object_get_virtual_method"));
        }
        return x;
    }();
    return e;
}

void nativeLog(int32_t level, const char* text) {
    if (!text) return;
    if (level >= 2) BL_ERROR("[mod] %s", text);
    else if (level == 1) BL_WARN("[mod] %s", text);
    else BL_INFO("[mod] %s", text);
}

void* nativeFindClass(const char* ns, const char* name) {
    if (!ns || !name) return nullptr;
    return il2cpp::findClassQuiet(ns, name);
}

int32_t nativeFieldOffset(void* cls, const char* name) {
    if (!cls || !name) return -1;
    return il2cpp::fieldOffset(static_cast<Il2CppClass*>(cls), name);
}

void* nativeFindMethod(void* cls, const char* name, int32_t argc) {
    if (!cls || !name) return nullptr;
    auto& a = il2cpp::api();
    return const_cast<MethodInfo*>(a.class_get_method_from_name(static_cast<Il2CppClass*>(cls), name, argc));
}

/**
 * Instala `replacement` (um ponteiro [UnmanagedCallersOnly] do C#) no metodo
 * e devolve o endereco do `original` dele. O C# le esse endereco a cada
 * chamada: um elo novo na cadeia (HookManager.h) troca o valor la dentro.
 * O espaco vive para sempre, como o hook.
 */
void** nativeInstallHook(void* method, void* replacement) {
    if (!method || !replacement) return nullptr;
    auto** slot = new void*(nullptr);
    if (!hook::install(static_cast<const MethodInfo*>(method), replacement, slot)) {
        delete slot;
        return nullptr;
    }
    return slot;
}

void* nativeMethodPointer(void* method) {
    return method ? il2cpp::methodPointer(static_cast<const MethodInfo*>(method)) : nullptr;
}

// So aloca, como o il2cpp_object_new: o construtor e chamado a parte.
void* nativeNewObject(void* cls) {
    return cls ? il2cpp::api().object_new(static_cast<Il2CppClass*>(cls)) : nullptr;
}

void* nativeFindNestedClass(void* outer, const char* name) {
    if (!outer || !name) return nullptr;
    return il2cpp::findNested(static_cast<Il2CppClass*>(outer), name);
}

/** A assinatura no formato do dump, com os nomes dos parametros (Signature.h). */
void* nativeFindMethodBySignature(void* cls, const char* signature) {
    if (!cls || !signature) return nullptr;
    il2cpp::Signature sig = il2cpp::parseSignature(signature);
    bool ambiguous = false;
    const MethodInfo* nameMismatch = nullptr;
    const MethodInfo* m = il2cpp::findMethodBySignature(static_cast<Il2CppClass*>(cls), sig, &ambiguous, &nameMismatch);
    if (!m && nameMismatch) {
        BL_WARN("dotnet: '%s': %s", signature, il2cpp::explainParamNames(nameMismatch, sig).c_str());
    }
    return const_cast<MethodInfo*>(m);
}

/** O tamanho do valor de um tipo (struct, enum, primitivo); -1 se e referencia. */
int32_t valueSizeOf(const Il2CppType* t) {
    const Extra& e = extra();
    if (!t || !e.classFromType) return -1;
    Il2CppClass* c = e.classFromType(t);
    auto& a = il2cpp::api();
    if (!c || !a.class_is_valuetype(c)) return -1;
    uint32_t align = 0;
    return a.class_value_size(c, &align);
}

/**
 * Confere os structs da assinatura contra o que o gerador supos. `sizes` tem
 * um por parametro e o ultimo e o retorno; -1 nao confere. O gerador passa o
 * tamanho de cada struct que ele mesmo montou: se o jogo mudou um struct, a
 * chamada e recusada em vez de ler a memoria errada.
 *
 * @return -1 se tudo bate; senao o indice do primeiro que difere.
 */
int32_t nativeCheckLayout(void* method, const int32_t* sizes, int32_t count) {
    auto& a = il2cpp::api();
    const auto* m = static_cast<const MethodInfo*>(method);
    if (!m || !sizes) return 0;
    const int32_t params = static_cast<int32_t>(a.method_get_param_count(m));
    if (count != params + 1) return 0;
    for (int32_t i = 0; i < count; ++i) {
        if (sizes[i] < 0) continue;
        const Il2CppType* t = i < params ? a.method_get_param(m, static_cast<uint32_t>(i)) : a.method_get_return_type(m);
        if (valueSizeOf(t) != sizes[i]) return i;
    }
    return -1;
}

int32_t nativeCallGame(void* fn, CallFrame* frame) { return callGame(fn, frame); }

const char* nativeLastCallError() { return lastCallError(); }

void* nativeFindField(void* cls, const char* name) {
    if (!cls || !name) return nullptr;
    return il2cpp::findField(static_cast<Il2CppClass*>(cls), name);
}

// Il2CppClass::static_fields no Unity 2021.3 (o `ldr x8, [x8, #0xb8]` do
// SaveWorldTilesFast, content/tiles/ModTileSave.cpp). Conferido uma vez em
// staticFastPathOk() contra o il2cpp_field_static_get_value.
constexpr size_t kClassStaticFields = 0xB8;
constexpr uint32_t kFieldAttributeStatic = 0x10;
constexpr uint32_t kFieldAttributeLiteral = 0x40;
constexpr size_t kThreadStaticOffset = static_cast<size_t>(-1);

void* staticAddressOf(FieldInfo* f) {
    auto& a = il2cpp::api();
    Il2CppClass* owner = a.field_get_parent(f);
    if (!owner) return nullptr;
    if (a.runtime_class_init) a.runtime_class_init(owner);
    const size_t offset = a.field_get_offset(f);
    if (offset == kThreadStaticOffset) return nullptr;
    auto* statics = *reinterpret_cast<uint8_t**>(reinterpret_cast<uint8_t*>(owner) + kClassStaticFields);
    return statics ? statics + offset : nullptr;
}

/**
 * O 0xB8 vale neste binario? Compara o endereco calculado com a copia do
 * il2cpp num estatico de referencia que nunca e nulo (Main.versionNumber,
 * uma string): ponteiro igual nao e coincidencia.
 */
bool staticFastPathOk() {
    static const bool ok = [] {
        Il2CppClass* main = il2cpp::findClassQuiet("Terraria", "Main");
        FieldInfo* f = main ? il2cpp::findField(main, "versionNumber") : nullptr;
        if (!f) {
            BL_WARN("dotnet: Main.versionNumber not found; static fields use the slow path");
            return false;
        }
        void* expected = nullptr;
        il2cpp::api().field_static_get_value(f, &expected);
        void* at = staticAddressOf(f);
        const bool same = expected && at && *static_cast<void**>(at) == expected;
        if (!same) BL_WARN("dotnet: Il2CppClass::static_fields is not at 0x%zx; static fields use the slow path", kClassStaticFields);
        return same;
    }();
    return ok;
}

void* nativeStaticFieldAddress(void* field) {
    auto* f = static_cast<FieldInfo*>(field);
    if (!f) return nullptr;
    const uint32_t flags = il2cpp::api().field_get_flags(f);
    if (!(flags & kFieldAttributeStatic) || (flags & kFieldAttributeLiteral)) return nullptr;
    if (!staticFastPathOk()) return nullptr;
    return staticAddressOf(f);
}

void nativeStaticFieldGet(void* field, void* value) {
    if (field && value) il2cpp::api().field_static_get_value(static_cast<FieldInfo*>(field), value);
}

// Campo de REFERENCIA recebe o proprio ponteiro; so campo de VALOR recebe o
// endereco (o mesmo que o il2cpp_field_static_set_value espera). Quem chama
// (Interop.cs) ja passa assim.
void nativeStaticFieldSet(void* field, void* value) {
    if (field) il2cpp::api().field_static_set_value(static_cast<FieldInfo*>(field), value);
}

void* nativeNewString(const uint16_t* chars, int32_t length) {
    const Extra& e = extra();
    if (!chars || length < 0) return nullptr;
    if (e.stringNewUtf16) return e.stringNewUtf16(chars, length);
    return nullptr;
}

int32_t nativeClassValueSize(void* cls) {
    if (!cls) return -1;
    uint32_t align = 0;
    return il2cpp::api().class_value_size(static_cast<Il2CppClass*>(cls), &align);
}

int32_t nativeIsInstance(void* obj, void* cls) {
    if (!obj || !cls) return 0;
    auto& a = il2cpp::api();
    Il2CppClass* c = a.object_get_class(static_cast<Il2CppObject*>(obj));
    return c && a.class_is_assignable_from(static_cast<Il2CppClass*>(cls), c) ? 1 : 0;
}

uint32_t nativeGcHandleNew(void* obj, int32_t pinned) {
    return obj ? il2cpp::api().gchandle_new(static_cast<Il2CppObject*>(obj), pinned != 0) : 0;
}

void* nativeGcHandleTarget(uint32_t handle) {
    return handle ? il2cpp::api().gchandle_get_target(handle) : nullptr;
}

void nativeGcHandleFree(uint32_t handle) {
    if (handle) il2cpp::api().gchandle_free(handle);
}

void* nativeNewArray(void* elementClass, int32_t length) {
    if (!elementClass || length < 0) return nullptr;
    return il2cpp::api().array_new(static_cast<Il2CppClass*>(elementClass), static_cast<uintptr_t>(length));
}

const char* nativeClassName(void* cls) {
    return cls ? il2cpp::api().class_get_name(static_cast<Il2CppClass*>(cls)) : "";
}

/** O metodo que roda de verdade para este objeto (o override, se houver). */
void* nativeVirtualMethod(void* obj, void* method) {
    const Extra& e = extra();
    if (!obj || !method || !e.objectGetVirtualMethod) return method;
    const MethodInfo* m = e.objectGetVirtualMethod(static_cast<Il2CppObject*>(obj), static_cast<const MethodInfo*>(method));
    return const_cast<MethodInfo*>(m ? m : static_cast<const MethodInfo*>(method));
}

bool fileExists(const char* path) {
    struct stat st{};
    return stat(path, &st) == 0 && S_ISREG(st.st_mode);
}

/** O mod no Mod Menu: nome do manifesto e o icone pequeno (ou o icon.png). */
void noteModForMenu(const std::string& mod) {
    const std::string root = mods::rootOf(mod);
    std::string icon;
    if (!root.empty()) {
        for (const char* name : {"icon_small.png", "icon.png"}) {
            const std::string candidate = root + "/" + name;
            if (fileExists(candidate.c_str())) { icon = candidate; break; }
        }
    }
    runtime::setModMenuInfo(mod, mods::displayName(mod), icon);
}

/**
 * Texto por cultura que vem do C#: "texto" sozinho vale para qualquer uma;
 * por cultura, "pt-BR\x1Ftexto\x1Een-US\x1Ftexto". Vazio = nenhum.
 */
std::vector<std::pair<std::string, std::string>> cultureTexts(const char* packed) {
    std::vector<std::pair<std::string, std::string>> out;
    if (!packed || !*packed) return out;
    const std::string all = packed;
    if (all.find('\x1F') == std::string::npos) {
        out.push_back({"", all});
        return out;
    }
    size_t start = 0;
    while (start <= all.size()) {
        size_t end = all.find('\x1E', start);
        if (end == std::string::npos) end = all.size();
        const std::string entry = all.substr(start, end - start);
        const size_t sep = entry.find('\x1F');
        if (sep != std::string::npos) out.push_back({entry.substr(0, sep), entry.substr(sep + 1)});
        start = end + 1;
    }
    return out;
}

/**
 * Um ModItem do C#: o tipo (ItemID.Count + a ordem), -1 se o mod ja tem um
 * item com esse nome, -2 se a textura (caminho absoluto) nao existe. Nome e
 * tooltip no formato do cultureTexts.
 */
int32_t nativeRegisterItem(const char* mod, const char* name, const char* texture,
                           const char* displayName, const char* tooltip) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (!fileExists(texture)) return -2;
    runtime::ModItemDef d;
    d.mod = mod;
    d.name = name;
    d.texture = texture;
    d.names = cultureTexts(displayName);
    d.tooltips = cultureTexts(tooltip);
    const int type = runtime::registerModItem(std::move(d));
    if (type >= 0) {
        runtime::markManagedItem(type);
        noteModForMenu(mod);
    }
    return type;
}

void nativePrepareItem(void* item, int32_t type) {
    runtime::prepareModItem(static_cast<Il2CppObject*>(item), type);
}

void nativeFinishItem(void* item, int32_t type) {
    runtime::finishModItem(static_cast<Il2CppObject*>(item), type);
}

/** O C# fica sabendo quando o jogo ja conhece os tipos (o SetStaticDefaults). */
void nativeOnItemsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::addItemsInstalledHook(reinterpret_cast<runtime::ItemsInstalledHook>(callback));
}

void nativeOnContentReady(void (*callback)()) {
    runtime::addContentReadyHook(callback);
}

int32_t nativeIsGameThread() {
    const int game = runtime::gameThreadId();
    return game != 0 && static_cast<int>(gettid()) == game ? 1 : 0;
}

void* nativeRequestModTexture(const char* file, const char* assetName) {
    if (!file || !assetName || !nativeIsGameThread()) return nullptr;
    static std::map<std::string, uint32_t> loaded;
    auto& a = il2cpp::api();
    auto it = loaded.find(assetName);
    if (it != loaded.end()) {
        if (Il2CppObject* asset = a.gchandle_get_target(it->second)) return asset;
    }
    int w = 0, h = 0;
    Il2CppObject* asset = runtime::content::loadTextureAsset(file, nullptr, 0, assetName, &w, &h);
    if (!asset) return nullptr;
    loaded[assetName] = a.gchandle_new(asset, false);
    return asset;
}

void* nativeRequestVanillaTexture(const char* name, int32_t* missingOnMobile) {
    if (missingOnMobile) *missingOnMobile = 0;
    if (!name || !nativeIsGameThread()) return nullptr;
    bool missing = false;
    Il2CppObject* asset = runtime::content::vanillaTextureAsset(name, &missing);
    if (missingOnMobile) *missingOnMobile = missing ? 1 : 0;
    return asset;
}

void* nativeTextureAssetValue(void* asset) {
    return runtime::content::textureAssetValue(static_cast<Il2CppObject*>(asset));
}

int32_t nativeModManifestField(const char* uid, int32_t field, char* out, int32_t size) {
    const mods::LoadedMod* mod = uid ? mods::find(uid) : nullptr;
    if (!mod) return -1;
    std::string value;
    switch (field) {
        case 0: value = mod->internalName; break;
        case 1: value = mod->displayName; break;
        case 2: value = mod->version; break;
        case 3:
            for (const std::string& dependency : mod->dependencies) {
                if (!value.empty()) value += '\n';
                value += dependency;
            }
            break;
        default: return -1;
    }
    if (out && size > 0) {
        const size_t n = std::min(value.size(), static_cast<size_t>(size - 1));
        std::memcpy(out, value.data(), n);
        out[n] = '\0';
    }
    return static_cast<int32_t>(value.size());
}

void* nativeLanguageText(const char* key, const char* packedTexts, int32_t replace) {
    if (!key || !*key) return nullptr;
    if (!packedTexts) return runtime::modLanguageText(key, nullptr, false);
    runtime::content::CultureNames names = cultureTexts(packedTexts);
    if (names.empty()) names.push_back({"", ""});
    return runtime::modLanguageText(key, &names, replace != 0);
}

uint32_t nativeGcHandleNewWeak(void* obj) {
    auto& a = il2cpp::api();
    return obj && a.gchandle_new_weakref ? a.gchandle_new_weakref(static_cast<Il2CppObject*>(obj), false) : 0;
}

void nativeOnItemData(int32_t (*save)(void*, const char*, void*), int32_t (*load)(void*, const char*)) {
    runtime::setManagedItemData(save, load);
}

void nativeWriteString(void* sink, const char* text) {
    if (sink) static_cast<std::string*>(sink)->assign(text ? text : "");
}

int32_t nativeNetInstall(void (*handler)(const uint8_t*, int32_t, int32_t)) {
    return runtime::installManagedNet(handler) ? 1 : 0;
}

int32_t nativeNetSend(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient) {
    return runtime::sendManagedPacket(data, length, toClient, ignoreClient) ? 1 : 0;
}

int32_t nativeNetMode() { return runtime::gameNetMode(); }

void nativeNetHello(const char* hello, const char* refusal) {
    runtime::setManagedHello(hello ? hello : "", cultureTexts(refusal));
}

struct ManagedDraw {
    int32_t (*pre)(void* args) = nullptr;
    void (*post)() = nullptr;
};

ManagedDraw g_managedDraw[2];

bool managedDrawPre(void* user, void* args) { return static_cast<ManagedDraw*>(user)->pre(args) != 0; }

void managedDrawPost(void* user) { static_cast<ManagedDraw*>(user)->post(); }

int32_t nativeItemDrawListen(int32_t kind, int32_t (*pre)(void* args), void (*post)(), uint8_t* active) {
    if (kind < 0 || kind > 1 || !pre || !post || !active || g_managedDraw[kind].pre) return 0;
    g_managedDraw[kind].pre = pre;
    g_managedDraw[kind].post = post;
    runtime::content::ItemDrawListener listener;
    listener.active = reinterpret_cast<const std::atomic<uint8_t>*>(active);
    listener.pre = managedDrawPre;
    listener.post = managedDrawPost;
    listener.user = &g_managedDraw[kind];
    if (runtime::content::addItemDrawListener(static_cast<runtime::content::ItemDrawKind>(kind), listener)) return 1;
    g_managedDraw[kind] = ManagedDraw{};
    return 0;
}

void nativeConsolePrint(int32_t kind, const char* text) {
    script::consolePrint(static_cast<char>(kind), text ? text : "");
}

void* nativeExpandChestItem(int32_t chest, int32_t slot) { return runtime::expandChestItem(chest, slot); }

int32_t nativeAllocateRarity() { return runtime::allocateModRarity(); }

int32_t nativeRarityCount() { return runtime::modRarityCount(); }

int32_t nativeRegisterPrefix(const char* mod, const char* name) {
    return mod && name ? runtime::registerModPrefix(mod, name) : -1;
}

int32_t nativePrefixCount() { return runtime::modPrefixCount(); }

int32_t nativeGrowEquipSets(const char* kind, int32_t from, int32_t to) { return runtime::growEquipSets(kind, from, to); }

void* nativeResizeArray(void* array, int32_t length) {
    if (!array || length < 0) return nullptr;
    return runtime::TypeTables::resizedCopy(static_cast<Il2CppArray*>(array), static_cast<uintptr_t>(length));
}

int32_t nativeRegisterBuff(const char* mod, const char* name, const char* texture, const char* displayName, const char* description) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (!fileExists(texture)) return -2;
    runtime::ModBuffDef d;
    d.mod = mod;
    d.name = name;
    d.texture = texture;
    d.names = cultureTexts(displayName);
    d.descriptions = cultureTexts(description);
    const int type = runtime::registerModBuff(std::move(d));
    if (type >= 0) noteModForMenu(mod);
    return type;
}

void nativeOnBuffsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::setBuffsInstalledHook(reinterpret_cast<runtime::BuffsInstalledHook>(callback));
}

void* nativeBuffIconLook() { return runtime::buffIconLook(); }

int32_t nativeOnNaturalRegen(float (*callback)(void* player, float regen)) {
    return callback && runtime::setNaturalRegenCallback(callback) ? 1 : 0;
}

int32_t nativeOnConsumeBait(int32_t (*callback)(void* player, void* bait)) {
    return callback && runtime::setConsumeBaitCallback(callback) ? 1 : 0;
}

int32_t nativeOnDyeReward(int32_t (*callback)(void* player, int32_t* pool, int32_t count, int32_t capacity)) {
    return callback && runtime::setDyeRewardCallback(callback) ? 1 : 0;
}

int32_t nativeStoreChestItem(int32_t chest, int32_t slot, void* item) {
    return runtime::storeChestItemData(chest, slot, static_cast<Il2CppObject*>(item)) ? 1 : 0;
}

const NativeApi kNativeApi{
    sizeof(NativeApi),
    nativeLog,
    nativeFindClass,
    nativeFieldOffset,
    nativeFindMethod,
    nativeInstallHook,
    nativeMethodPointer,
    nativeNewObject,
    nativeFindNestedClass,
    nativeFindMethodBySignature,
    nativeCheckLayout,
    nativeCallGame,
    nativeLastCallError,
    nativeFindField,
    nativeStaticFieldAddress,
    nativeStaticFieldGet,
    nativeStaticFieldSet,
    nativeNewString,
    nativeClassValueSize,
    nativeIsInstance,
    nativeGcHandleNew,
    nativeGcHandleTarget,
    nativeGcHandleFree,
    nativeNewArray,
    nativeClassName,
    nativeVirtualMethod,
    nativeRegisterItem,
    nativePrepareItem,
    nativeFinishItem,
    nativeOnItemsInstalled,
    nativeOnContentReady,
    nativeIsGameThread,
    nativeRequestModTexture,
    nativeRequestVanillaTexture,
    nativeTextureAssetValue,
    nativeModManifestField,
    nativeLanguageText,
    nativeGcHandleNewWeak,
    nativeOnItemData,
    nativeWriteString,
    nativeNetInstall,
    nativeNetSend,
    nativeNetMode,
    nativeNetHello,
    nativeItemDrawListen,
    nativeConsolePrint,
    nativeExpandChestItem,
    nativeStoreChestItem,
    nativeAllocateRarity,
    nativeRarityCount,
    nativeRegisterPrefix,
    nativePrefixCount,
    nativeGrowEquipSets,
    nativeResizeArray,
    nativeRegisterBuff,
    nativeOnBuffsInstalled,
    nativeBuffIconLook,
    nativeOnNaturalRegen,
    nativeOnConsumeBait,
    nativeOnDyeReward,
};

} // namespace

const void* nativeApiTable() { return &kNativeApi; }

} // namespace bl::dotnet
