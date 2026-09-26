#include "script/bridge/Members.h"

#if BL_HAVE_QUICKJS
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "core/Log.h"

#include <cctype>
#include <cstdlib>
#include <cstring>
#include <unordered_map>
#include <vector>

namespace bl::script {

namespace {

constexpr uint32_t kFieldStatic = 0x0010;  // FIELD_ATTRIBUTE_STATIC

struct Key {
    Il2CppClass* cls;
    JSAtom atom;
    Space space;
    bool operator==(const Key& o) const {
        return cls == o.cls && atom == o.atom && space == o.space;
    }
};

struct KeyHash {
    size_t operator()(const Key& k) const {
        size_t h = reinterpret_cast<uintptr_t>(k.cls);
        h ^= static_cast<size_t>(k.atom) * 0x9E3779B97F4A7C15ull;
        return h ^ static_cast<size_t>(k.space);
    }
};

/**
 * Campo pelo nome, respeitando se e estatico.
 *
 * Antes qualquer campo servia: `Terraria.Player.statLife` lia um campo de
 * INSTANCIA como estatico, e `player.algumEstatico` usava o offset de um
 * estatico como se fosse do objeto. Os dois devolviam lixo sem erro.
 */
FieldInfo* findFieldIn(Il2CppClass* cls, const char* name, bool wantStatic) {
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        FieldInfo* f = a.class_get_field_from_name(c, name);
        if (!f) continue;
        if (!a.field_get_flags) return f;
        if (((a.field_get_flags(f) & kFieldStatic) != 0) == wantStatic) return f;
    }
    return nullptr;
}

const MethodInfo* uniqueMethodNamed(Il2CppClass* cls, const std::string& name) {
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        void* iter = nullptr;
        while (const MethodInfo* m = a.class_get_methods(c, &iter)) {
            if (name == a.method_get_name(m)) return m;
        }
    }
    return nullptr;
}

Member resolve(JSContext* ctx, Il2CppClass* cls, JSAtom atom, Space space, JSClassID protoId) {
    Member m;

    JSValue proto = JS_GetClassProto(ctx, protoId);
    int onProto = JS_HasProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    if (onProto > 0) {
        m.proto = true;
        return m;
    }

    auto& a = il2cpp::api();
    std::string name = atomName(ctx, atom);

    if (name.find('(') != std::string::npos) {
        m.signature = true;
        il2cpp::Signature sig = il2cpp::parseSignature(name);
        if (sig.valid) {
            bool ambiguous = false;
            m.method = il2cpp::findMethodBySignature(cls, sig, &ambiguous);
        }
        return m;
    }

    if (FieldInfo* f = findFieldIn(cls, name.c_str(), space == Space::Static)) {
        m.field = f;
        m.type = &describe(a.field_get_type(f));
        size_t off = a.field_get_offset(f);
        m.offset = space == Space::Struct ? structFieldOffset(cls, off) : off;
        return m;
    }

    // Propriedade C#: `Main.myPlayer` e get_myPlayer().
    m.getter = a.class_get_method_from_name(cls, ("get_" + name).c_str(), 0);
    m.setter = a.class_get_method_from_name(cls, ("set_" + name).c_str(), 1);

    // O nome puro vira metodo (Item.SetDefaults, item.TurnToAir,
    // entry.Info.Add) so se for unico. Com overloads, quem acessa recebe o
    // erro listando todos. Em instancia tambem: e como o codigo do ExMod (e do
    // tModLoader) chama — sem isto, `entry.Info.Add(x)` era "not a function".
    // E em struct: `proj.localAI.get_Item(0)` era undefined.
    if (!m.getter) {
        m.overloads = static_cast<int>(il2cpp::listOverloads(cls, name).size());
        if (m.overloads == 1) m.method = uniqueMethodNamed(cls, name);
    }
    // `SpriteFont.Glyph`: o C# nao deixa um membro e um tipo aninhado com o
    // mesmo nome, entao so resta olhar aqui quando nada acima casou.
    if (space == Space::Static && !m.getter && !m.setter && !m.method && m.overloads == 0) {
        m.nested = il2cpp::findNested(cls, name);
    }
    return m;
}

} // namespace

const Member& member(JSContext* ctx, Il2CppClass* cls, JSAtom atom, Space space,
                     JSClassID protoId) {
    static std::unordered_map<Key, Member, KeyHash> cache;
    const Key k{cls, atom, space};
    auto it = cache.find(k);
    if (it != cache.end()) return it->second;

    Member m = resolve(ctx, cls, atom, space, protoId);
    // O atomo e a chave: se o QuickJS o liberasse, o mesmo numero poderia
    // voltar para OUTRO nome (chave montada em tempo de execucao, `obj[k]`) e
    // o cache responderia com o membro errado. Segurar uma referencia impede.
    JS_DupAtom(ctx, atom);
    return cache.emplace(k, m).first->second;
}

// ------------------------------ indexador ------------------------------

namespace {

bool sameType(const TypeDesc& a, const TypeDesc& b) {
    return a.prim == b.prim && a.cls == b.cls && a.size == b.size;
}

/** get_Item/set_Item com o indice int no 1o parametro; senao nullptr. */
const MethodInfo* itemMethod(Il2CppClass* cls, const char* name, int params) {
    auto& a = il2cpp::api();
    const MethodInfo* m = a.class_get_method_from_name(cls, name, params);
    if (!m || !a.method_get_param) return nullptr;
    const TypeDesc& idx = describe(a.method_get_param(m, 0));
    return idx.prim == Prim::I32 && !idx.byRef ? m : nullptr;
}

/**
 * Modo CAMPOS: os campos de instancia `<prefixo><n>` (n = 0..N-1, um prefixo
 * so, mesmo tipo, um logo depois do outro) e o tipo que o get_Item devolve.
 * `Length` e `value` nao tem numero no fim e ficam de fora. Qualquer coisa
 * fora disso (KeyboardState: `keys0..7` sao palavras de bits, e o indexador
 * devolve outra coisa) cai no modo METODOS.
 */
bool planFields(Il2CppClass* cls, const TypeDesc& ret, Indexer* ix) {
    auto& a = il2cpp::api();
    if (!a.class_get_fields || !a.field_get_name || !a.field_get_offset) return false;
    std::vector<FieldInfo*> byIndex;
    std::string prefix;
    void* it = nullptr;
    while (FieldInfo* f = a.class_get_fields(cls, &it)) {
        if (a.field_get_flags && (a.field_get_flags(f) & kFieldStatic)) continue;
        const char* n = a.field_get_name(f);
        const size_t len = n ? std::strlen(n) : 0;
        size_t digits = 0;
        while (digits < len && std::isdigit(static_cast<unsigned char>(n[len - 1 - digits]))) ++digits;
        if (digits == 0 || digits == len || digits > 6) continue;
        const std::string pre(n, len - digits);
        if (prefix.empty()) prefix = pre;
        else if (pre != prefix) return false;
        const size_t idx = static_cast<size_t>(std::strtoul(n + len - digits, nullptr, 10));
        if (idx >= byIndex.size()) byIndex.resize(idx + 1, nullptr);
        if (byIndex[idx]) return false;
        byIndex[idx] = f;
    }
    if (byIndex.empty() || !byIndex[0]) return false;

    const TypeDesc& elem = describe(a.field_get_type(byIndex[0]));
    if (!elem.size || !sameType(elem, ret)) return false;
    const size_t stride = elem.byValue ? elem.size : sizeof(void*);
    const size_t base = a.field_get_offset(byIndex[0]);
    for (size_t i = 0; i < byIndex.size(); ++i) {
        FieldInfo* f = byIndex[i];
        if (!f || !sameType(describe(a.field_get_type(f)), elem)) return false;
        if (a.field_get_offset(f) != base + i * stride) return false;
    }
    ix->elem = &elem;
    ix->offset = structFieldOffset(cls, base);
    ix->stride = stride;
    ix->count = static_cast<int>(byIndex.size());
    return true;
}

Indexer planIndexer(Il2CppClass* cls) {
    auto& a = il2cpp::api();
    Indexer ix;
    const MethodInfo* get = itemMethod(cls, "get_Item", 1);
    if (!get || !a.method_get_return_type) return ix;
    const TypeDesc& ret = describe(a.method_get_return_type(get));
    if (planFields(cls, ret, &ix)) return ix;
    // `ref T get_Item` sem os campos a vista: nao ha onde escrever com seguranca.
    if (ret.byRef) return ix;
    ix.get = get;
    ix.set = itemMethod(cls, "set_Item", 2);
    const MethodInfo* len = a.class_get_method_from_name(cls, "get_Length", 0);
    if (len && describe(a.method_get_return_type(len)).prim == Prim::I32) ix.length = len;
    return ix;
}

} // namespace

const Indexer* indexerOf(Il2CppClass* cls) {
    static std::unordered_map<Il2CppClass*, Indexer> cache;
    auto it = cache.find(cls);
    if (it == cache.end()) it = cache.emplace(cls, planIndexer(cls)).first;
    const Indexer& ix = it->second;
    return ix.fields() || ix.get ? &ix : nullptr;
}

/*
 * Antes cada `arr[i]` virava C string, std::string e strtol. No QuickJS um
 * indice pequeno ja E um atomo inteiro: bit 31 ligado + o valor. Essa
 * codificacao e interna, e o QuickJS e baixado no build (nao fica versionado
 * aqui) — entao ela e CONFERIDA uma vez contra a API publica
 * (JS_NewAtomUInt32). Se um dia mudar, cai no caminho por texto em vez de ler
 * o indice errado. (JS_AtomToValue nao serve: monta uma string para atomo
 * inteiro.)
 */
int64_t indexOf(JSContext* ctx, JSAtom atom) {
    constexpr uint32_t kTagInt = 1u << 31;
    static int tagged = -1;
    if (tagged < 0) {
        const JSAtom probe = JS_NewAtomUInt32(ctx, 12345);
        tagged = probe == (kTagInt | 12345u) ? 1 : 0;
        JS_FreeAtom(ctx, probe);
        if (!tagged) BL_WARN("arrays: codificacao de atomo inteiro mudou; indice por texto");
    }
    if (tagged) return (atom & kTagInt) ? static_cast<int64_t>(atom & ~kTagInt) : -1;

    const char* key = JS_AtomToCString(ctx, atom);
    if (!key) return -1;
    char* end = nullptr;
    const long idx = std::strtol(key, &end, 10);
    const bool ok = end && *end == '\0' && end != key && idx >= 0;
    JS_FreeCString(ctx, key);
    return ok ? idx : -1;
}

JSValue protoGet(JSContext* ctx, JSClassID id, JSAtom atom) {
    JSValue proto = JS_GetClassProto(ctx, id);
    JSValue v = JS_GetProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    return v;
}

std::string atomName(JSContext* ctx, JSAtom atom) {
    const char* s = JS_AtomToCString(ctx, atom);
    std::string out = s ? s : "";
    if (s) JS_FreeCString(ctx, s);
    return out;
}

} // namespace bl::script
#endif
