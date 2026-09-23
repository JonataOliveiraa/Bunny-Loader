#include "script/Members.h"

#if BL_HAVE_QUICKJS
#include "il2cpp/Api.h"
#include "il2cpp/Signature.h"

#include <unordered_map>

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

    // So na classe o nome puro vira metodo (Item.SetDefaults), e so se for
    // unico. Com overloads, quem acessa recebe o erro listando todos.
    if (space == Space::Static && !m.getter) {
        m.overloads = static_cast<int>(il2cpp::listOverloads(cls, name).size());
        if (m.overloads == 1) m.method = uniqueMethodNamed(cls, name);
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
