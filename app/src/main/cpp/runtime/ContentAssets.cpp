#include "runtime/ContentAssets.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#include <cstdio>
#include <cstring>

namespace bl::runtime::content {

namespace {

struct Refs {
    bool tried = false, ok = false;
    Il2CppClass* byteCls = nullptr;
    Il2CppClass* unityTex = nullptr;
    Il2CppClass* gameTex = nullptr;
    Il2CppClass* assetCls = nullptr;          // Asset<Texture2D>
    Il2CppClass* localizedTextCls = nullptr;
    const MethodInfo* unityCtor = nullptr;
    const MethodInfo* loadImage = nullptr;
    const MethodInfo* setFilterMode = nullptr;
    const MethodInfo* getWidth = nullptr;
    const MethodInfo* getHeight = nullptr;
    const MethodInfo* gameCtor = nullptr;
    const MethodInfo* assetCtor = nullptr;
    const MethodInfo* assetSubmit = nullptr;
    const MethodInfo* localizedTextCtor = nullptr;
    const MethodInfo* activeCulture = nullptr;
    const MethodInfo* cultureName = nullptr;
    const MethodInfo* arraySet = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    auto& a = api();
    auto sig = [](Il2CppClass* c, const char* s) -> const MethodInfo* {
        return c ? findMethodBySignature(c, parseSignature(s)) : nullptr;
    };
    Il2CppClass* conv = findClass({"UnityEngine", "ImageConversion", {}});
    Il2CppClass* unityTexBase = findClass({"UnityEngine", "Texture", {}});
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* language = findClass({"Terraria.Localization", "Language", {}});
    Il2CppClass* culture = findClass({"Terraria.Localization", "GameCulture", {}});
    Il2CppClass* array = findClass({"System", "Array", {}});
    r.byteCls = findClass({"System", "Byte", {}});
    r.unityTex = findClass({"UnityEngine", "Texture2D", {}});
    r.gameTex = findClass({"Microsoft.Xna.Framework.Graphics", "Texture2D", {}});
    r.localizedTextCls = findClass({"Terraria.Localization", "LocalizedText", {}});

    // Asset<Texture2D> e generico: a classe fechada sai de uma tabela que o
    // jogo ja encheu (TextureAssets.Item e um Asset<Texture2D>[]). Chamado na
    // instalacao do conteudo, quando ela ja existe.
    Il2CppObject* items = nullptr;
    if (FieldInfo* f = textures ? findField(textures, "Item") : nullptr) a.field_static_get_value(f, &items);
    if (items) r.assetCls = a.class_get_element_class(a.object_get_class(items));
    r.unityCtor = sig(r.unityTex, "void .ctor(int width, int height)");
    r.loadImage = sig(conv, "bool LoadImage(Texture2D tex, byte[] data, bool markNonReadable)");
    r.setFilterMode = unityTexBase ? a.class_get_method_from_name(unityTexBase, "set_filterMode", 1) : nullptr;
    r.getWidth = unityTexBase ? a.class_get_method_from_name(unityTexBase, "get_width", 0) : nullptr;
    r.getHeight = unityTexBase ? a.class_get_method_from_name(unityTexBase, "get_height", 0) : nullptr;
    r.gameCtor = sig(r.gameTex, "void .ctor(Texture2D texture)");
    r.assetCtor = r.assetCls ? a.class_get_method_from_name(r.assetCls, ".ctor", 1) : nullptr;
    r.assetSubmit = r.assetCls ? a.class_get_method_from_name(r.assetCls, "SubmitLoadedContent", 2) : nullptr;
    r.localizedTextCtor = sig(r.localizedTextCls, "void .ctor(string key, string text)");
    r.activeCulture = language ? a.class_get_method_from_name(language, "get_ActiveCulture", 0) : nullptr;
    r.cultureName = culture ? a.class_get_method_from_name(culture, "get_Name", 0) : nullptr;
    r.arraySet = sig(array, "void SetValue(object value, int index)");

    r.ok = r.byteCls && r.unityTex && r.gameTex && r.assetCls && r.localizedTextCls && r.unityCtor &&
           r.loadImage && r.gameCtor && r.assetCtor && r.assetSubmit && r.localizedTextCtor &&
           r.arraySet && a.array_new;
    if (!r.ok) {
        BL_ERROR("conteudo de mod: refs faltando (Asset<Texture2D>=%p LoadImage=%p "
                 "LocalizedText.ctor=%p SetValue=%p)", (void*)r.assetCls, (void*)r.loadImage,
                 (void*)r.localizedTextCtor, (void*)r.arraySet);
    }
    return r;
}

/**
 * true se rodou sem excecao; o retorno vai em `ret`. As duas coisas separadas
 * de proposito: metodo `void` devolve nulo do runtime_invoke, e tratar nulo
 * como falha fazia todo construtor "falhar" com sucesso.
 */
bool invoke(const MethodInfo* m, void* self, void** args, const char* what,
            Il2CppObject** ret = nullptr) {
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(m, self, args, &exc);
    if (exc) {
        BL_ERROR("conteudo de mod: %s lancou excecao", what);
        return false;
    }
    if (ret) *ret = r;
    return true;
}

Il2CppObject* invokeValue(const MethodInfo* m, void* self, void** args, const char* what) {
    Il2CppObject* r = nullptr;
    return invoke(m, self, args, what, &r) ? r : nullptr;
}

int unboxInt(Il2CppObject* o) {
    return o ? *reinterpret_cast<int*>(reinterpret_cast<char*>(o) + sizeof(Il2CppObject)) : 0;
}

Il2CppArray* readBytes(const std::string& path, const unsigned char* data, size_t size) {
    auto& a = il2cpp::api();
    const Refs& r = refs();
    if (data) {
        Il2CppArray* bytes = a.array_new(r.byteCls, size);
        if (bytes) std::memcpy(arrayData(bytes), data, size);
        return bytes;
    }
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) { BL_ERROR("conteudo de mod: textura nao abre: %s", path.c_str()); return nullptr; }
    std::fseek(f, 0, SEEK_END);
    const long n = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    Il2CppArray* bytes = n > 0 ? a.array_new(r.byteCls, static_cast<uintptr_t>(n)) : nullptr;
    const size_t got = bytes ? std::fread(arrayData(bytes), 1, static_cast<size_t>(n), f) : 0;
    std::fclose(f);
    if (!bytes || got != static_cast<size_t>(n)) {
        BL_ERROR("conteudo de mod: textura ilegivel: %s", path.c_str());
        return nullptr;
    }
    return bytes;
}

} // namespace

Il2CppObject* loadTextureAsset(const std::string& path, const unsigned char* data, size_t size,
                               const std::string& assetName, int* width, int* height) {
    auto& a = il2cpp::api();
    const Refs& r = refs();
    if (!r.ok) return nullptr;
    Il2CppArray* bytes = readBytes(path, data, size);
    if (!bytes) return nullptr;

    Il2CppObject* ut = a.object_new(r.unityTex);
    int two = 2;
    void* c1[2] = {&two, &two};
    if (!invoke(r.unityCtor, ut, c1, "Texture2D(2,2)")) return nullptr;
    uint8_t nonReadable = 0;
    void* c2[3] = {ut, bytes, &nonReadable};
    Il2CppObject* ok = invokeValue(r.loadImage, nullptr, c2, "ImageConversion.LoadImage");
    if (!ok || !*(reinterpret_cast<uint8_t*>(ok) + sizeof(Il2CppObject))) {
        BL_ERROR("conteudo de mod: a Unity nao decodificou a textura de %s", assetName.c_str());
        return nullptr;
    }
    // Pixel art: vizinho-mais-proximo. A Unity nasce bilinear e a textura
    // sairia borrada ao lado das do jogo.
    if (r.setFilterMode) {
        int point = 0;   // FilterMode.Point
        void* c3[1] = {&point};
        invoke(r.setFilterMode, ut, c3, "Texture.filterMode");
    }
    if (r.getWidth && width) *width = unboxInt(invokeValue(r.getWidth, ut, nullptr, "Texture.width"));
    if (r.getHeight && height) *height = unboxInt(invokeValue(r.getHeight, ut, nullptr, "Texture.height"));

    Il2CppObject* gt = a.object_new(r.gameTex);
    void* c4[1] = {ut};
    if (!invoke(r.gameCtor, gt, c4, "Texture2D do jogo")) return nullptr;

    Il2CppObject* asset = a.object_new(r.assetCls);
    void* c5[1] = {a.string_new(assetName.c_str())};
    if (!invoke(r.assetCtor, asset, c5, "Asset.ctor")) return nullptr;
    void* c6[2] = {gt, nullptr};
    if (!invoke(r.assetSubmit, asset, c6, "Asset.SubmitLoadedContent")) return nullptr;
    return asset;
}

std::string textForCulture(const CultureNames& names, const std::string& fallback) {
    const Refs& r = refs();
    std::string culture;
    if (r.activeCulture && r.cultureName) {
        Il2CppObject* c = invokeValue(r.activeCulture, nullptr, nullptr, "Language.ActiveCulture");
        auto* s = c ? reinterpret_cast<Il2CppString*>(invokeValue(r.cultureName, c, nullptr, "GameCulture.Name"))
                    : nullptr;
        if (s) for (int i = 0; i < s->length; ++i) culture += static_cast<char>(s->chars[i]);
    }
    const std::string language = culture.substr(0, culture.find('-'));
    const std::string* match = nullptr;
    for (const auto& n : names) if (n.first == culture) match = &n.second;
    if (!match) for (const auto& n : names) if (n.first.substr(0, n.first.find('-')) == language) match = &n.second;
    if (!match) for (const auto& n : names) if (n.first.empty() || n.first == "en-US") match = &n.second;
    if (!match && !names.empty()) match = &names.front().second;
    return match ? *match : fallback;
}

namespace {

/**
 * O texto tambem vai para o dicionario do idioma (LanguageManager.Instance.
 * _localizedTexts), sob a mesma chave. O jogo guarda o nome de item e de NPC
 * em tabelas por tipo, mas partes dele buscam pela CHAVE: a plaquinha do
 * Bestiario (Language.GetTextValue("NPCName.X")) mostrava a chave crua.
 */
void registerLanguageKey(Il2CppString* key, Il2CppObject* text) {
    auto& a = il2cpp::api();
    static FieldInfo* instance = nullptr;
    static int32_t offTexts = -2;
    static const MethodInfo* setItem = nullptr;
    if (offTexts == -2) {
        Il2CppClass* lm = il2cpp::findClass({"Terraria.Localization", "LanguageManager", {}});
        instance = lm ? il2cpp::findField(lm, "Instance") : nullptr;
        offTexts = lm ? il2cpp::fieldOffset(lm, "_localizedTexts") : -1;
    }
    Il2CppObject* manager = nullptr;
    if (instance) a.field_static_get_value(instance, &manager);
    if (!manager || offTexts < 0) return;
    Il2CppObject* dict = *reinterpret_cast<Il2CppObject**>(reinterpret_cast<char*>(manager) + offTexts);
    if (!dict) return;
    if (!setItem) setItem = a.class_get_method_from_name(a.object_get_class(dict), "set_Item", 2);
    if (!setItem) return;
    void* args[2] = {key, text};
    invoke(setItem, dict, args, "LanguageManager._localizedTexts[chave]");
}

} // namespace

Il2CppObject* makeLocalizedText(const std::string& key, const std::string& text) {
    auto& a = il2cpp::api();
    const Refs& r = refs();
    if (!r.ok) return nullptr;
    Il2CppObject* t = a.object_new(r.localizedTextCls);
    Il2CppString* k = a.string_new(key.c_str());
    void* c[2] = {k, a.string_new(text.c_str())};
    if (!invoke(r.localizedTextCtor, t, c, "LocalizedText.ctor")) return nullptr;
    registerLanguageKey(k, t);
    return t;
}

bool setTableElement(FieldInfo* table, int index, Il2CppObject* value) {
    const Refs& r = refs();
    if (!r.ok || !table) return false;
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(table, &arr);
    if (!arr || index < 0 || static_cast<uintptr_t>(index) >= arr->length) return false;
    void* sv[2] = {value, &index};
    return invoke(r.arraySet, arr, sv, "Array.SetValue");
}

} // namespace bl::runtime::content
