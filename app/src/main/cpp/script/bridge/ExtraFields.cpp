#include "script/bridge/ExtraFields.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/bridge/Bridge.h"
#include <unordered_map>
#include <vector>

namespace bl::script {

namespace {

struct Def {
    Il2CppClass* cls;
    JSAtom atom;
};
std::vector<Def> g_defs;

// bl.defineMethod: (classe, nome) -> funcao JS, para sempre.
struct MethodDef {
    Il2CppClass* cls;
    JSAtom atom;
    JSValue fn;
};
std::vector<MethodDef> g_methods;
// Os de `{ override: true }`: vencem o membro do jogo de mesmo nome. Lista a
// parte, conferida em toda leitura de membro: quase sempre vazia.
std::vector<MethodDef> g_overrides;

// bl.defineProperty: (classe, nome) -> getter e setter JS, para sempre.
struct AccessorDef {
    Il2CppClass* cls;
    JSAtom atom;
    JSValue get;
    JSValue set;   // undefined: so leitura
};
std::vector<AccessorDef> g_accessors;

const AccessorDef* findAccessor(Il2CppClass* cls, JSAtom atom) {
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c && !g_accessors.empty(); c = a.class_get_parent(c)) {
        for (const AccessorDef& d : g_accessors) {
            if (d.cls == c && d.atom == atom) return &d;
        }
    }
    return nullptr;
}

struct Entry {
    uint32_t weak = 0;             // referencia fraca ao objeto
    JSValue fields = JS_UNDEFINED; // objeto JS com os campos
};
std::unordered_map<Il2CppObject*, Entry> g_entries;
JSContext* g_ctx = nullptr;

// A varredura das entradas mortas roda a cada tantas entradas novas: o custo
// fica proporcional ao que entra, e a tabela nao cresce sem limite.
constexpr size_t kSweepEvery = 1024;
size_t g_sinceSweep = 0;

bool alive(Il2CppObject* obj, const Entry& e) {
    return e.weak && il2cpp::api().gchandle_get_target(e.weak) == obj;
}

void drop(Entry& e) {
    if (e.weak) il2cpp::api().gchandle_free(e.weak);
    if (g_ctx) JS_FreeValue(g_ctx, e.fields);
    e.weak = 0;
    e.fields = JS_UNDEFINED;
}

void sweep() {
    size_t before = g_entries.size();
    for (auto it = g_entries.begin(); it != g_entries.end();) {
        if (alive(it->first, it->second)) { ++it; continue; }
        drop(it->second);
        it = g_entries.erase(it);
    }
    if (before != g_entries.size()) {
        BL_DEBUG("campos extras: %zu de %zu entrada(s) de objeto recolhido descartada(s)",
                 before - g_entries.size(), before);
    }
}

/** A entrada viva do objeto, ou nullptr. A morta e descartada aqui. */
Entry* find(Il2CppObject* obj) {
    auto it = g_entries.find(obj);
    if (it == g_entries.end()) return nullptr;
    if (alive(obj, it->second)) return &it->second;
    drop(it->second);
    g_entries.erase(it);
    return nullptr;
}

JSValue js_defineField(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    Il2CppClass* cls = argc >= 2 ? classFromJS(argv[0]) : nullptr;
    const char* name = argc >= 2 ? JS_ToCString(ctx, argv[1]) : nullptr;
    if (!cls || !name) {
        if (name) JS_FreeCString(ctx, name);
        return JS_ThrowTypeError(ctx, "bl.defineField(Classe, nome): passe uma classe do jogo e o nome");
    }
    if (!il2cpp::api().gchandle_new_weakref) {
        JS_FreeCString(ctx, name);
        return JS_ThrowInternalError(ctx, "bl.defineField: este runtime nao tem referencia fraca");
    }
    JSAtom atom = JS_NewAtom(ctx, name);
    JS_FreeCString(ctx, name);
    for (const Def& d : g_defs) {
        if (d.cls == cls && d.atom == atom) { JS_FreeAtom(ctx, atom); return JS_UNDEFINED; }
    }
    g_defs.push_back({cls, atom});   // o atomo fica para sempre, como a definicao
    return JS_UNDEFINED;
}

/**
 * bl.defineMethod(Classe, nome, fn[, { override: true }]): `obj.nome(...)`
 * num objeto do jogo chama fn com `this` = o objeto. Nao muda o jogo: e so do
 * lado do JS, como o defineField. Nome que a classe ja tem (campo, metodo)
 * vence; com `override`, vence o do mod (o `GetArmorPenetration(DamageClass)`
 * do tModLoader sobre o `GetArmorPenetration(bool melee)` do jogo, que segue
 * acessivel pela assinatura).
 */
JSValue js_defineMethod(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    Il2CppClass* cls = argc >= 3 ? classFromJS(argv[0]) : nullptr;
    const char* name = argc >= 3 ? JS_ToCString(ctx, argv[1]) : nullptr;
    if (!cls || !name || !JS_IsFunction(ctx, argv[2])) {
        if (name) JS_FreeCString(ctx, name);
        return JS_ThrowTypeError(ctx, "bl.defineMethod(Classe, nome, funcao[, { override }])");
    }
    bool override = false;
    if (argc >= 4 && JS_IsObject(argv[3])) {
        JSValue o = JS_GetPropertyStr(ctx, argv[3], "override");
        override = JS_ToBool(ctx, o) > 0;
        JS_FreeValue(ctx, o);
    }
    JSAtom atom = JS_NewAtom(ctx, name);
    JS_FreeCString(ctx, name);
    std::vector<MethodDef>& list = override ? g_overrides : g_methods;
    for (MethodDef& d : list) {
        if (d.cls == cls && d.atom == atom) {
            JS_FreeValue(ctx, d.fn);
            d.fn = JS_DupValue(ctx, argv[2]);
            JS_FreeAtom(ctx, atom);
            return JS_UNDEFINED;
        }
    }
    list.push_back({cls, atom, JS_DupValue(ctx, argv[2])});
    return JS_UNDEFINED;
}

/**
 * bl.defineProperty(Classe, nome, getter, setter): `obj.nome` num objeto do
 * jogo chama getter(), e `obj.nome = v` chama setter(v), com `this` = o
 * objeto. E o campo extra com codigo: o DamageType do item liga as flags do
 * jogo ao ser escrito. Sem setter, escrever e erro. Nome que a classe ja tem
 * vence, como no defineMethod.
 */
JSValue js_defineProperty(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    Il2CppClass* cls = argc >= 3 ? classFromJS(argv[0]) : nullptr;
    const char* name = argc >= 3 ? JS_ToCString(ctx, argv[1]) : nullptr;
    const bool hasSetter = argc >= 4 && !JS_IsUndefined(argv[3]);
    if (!cls || !name || !JS_IsFunction(ctx, argv[2]) || (hasSetter && !JS_IsFunction(ctx, argv[3]))) {
        if (name) JS_FreeCString(ctx, name);
        return JS_ThrowTypeError(ctx, "bl.defineProperty(Classe, nome, getter[, setter])");
    }
    JSAtom atom = JS_NewAtom(ctx, name);
    JS_FreeCString(ctx, name);
    const JSValue set = hasSetter ? JS_DupValue(ctx, argv[3]) : JS_UNDEFINED;
    for (AccessorDef& d : g_accessors) {
        if (d.cls == cls && d.atom == atom) {
            JS_FreeValue(ctx, d.get);
            JS_FreeValue(ctx, d.set);
            d.get = JS_DupValue(ctx, argv[2]);
            d.set = set;
            JS_FreeAtom(ctx, atom);
            return JS_UNDEFINED;
        }
    }
    g_accessors.push_back({cls, atom, JS_DupValue(ctx, argv[2]), set});
    return JS_UNDEFINED;
}

// data[0] = a funcao do mod, data[1] = o objeto do jogo.
JSValue callBound(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv, int, JSValueConst* data) {
    return JS_Call(ctx, data[0], data[1], argc, argv);
}

/** O endereco do objeto do jogo, como numero (cabe num double: 48 bits). */
JSValue js_addressOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    Il2CppObject* o = argc >= 1 ? objectFromJS(argv[0]) : nullptr;
    if (!o) return JS_ThrowTypeError(ctx, "bl.addressOf(objeto do jogo)");
    return JS_NewFloat64(ctx, static_cast<double>(reinterpret_cast<uintptr_t>(o)));
}

/**
 * O objeto de volta pelo endereco — so um que tem campo extra e continua vivo.
 * E o que deixa a instancia de mod apontar para a entidade sem segura-la.
 */
JSValue js_objectAt(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    double d = 0;
    if (argc < 1 || JS_ToFloat64(ctx, &d, argv[0]) < 0) return JS_EXCEPTION;
    auto* o = reinterpret_cast<Il2CppObject*>(static_cast<uintptr_t>(d));
    if (!o || !find(o)) return JS_UNDEFINED;
    return makeNativeObject(ctx, o);
}

} // namespace

bool isExtraField(Il2CppClass* cls, JSAtom atom) {
    if (g_defs.empty()) return false;
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        for (const Def& d : g_defs) {
            if (d.cls == c && d.atom == atom) return true;
        }
    }
    return false;
}

bool extraMethodGet(JSContext* ctx, Il2CppClass* cls, JSValueConst self, JSAtom atom, JSValue* out) {
    if (g_methods.empty()) return false;
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        for (const MethodDef& d : g_methods) {
            if (d.cls != c || d.atom != atom) continue;
            JSValueConst data[2] = {d.fn, self};
            *out = JS_NewCFunctionData(ctx, callBound, 0, 0, 2, const_cast<JSValue*>(data));
            return true;
        }
    }
    return false;
}

bool extraMethodOverride(JSContext* ctx, Il2CppClass* cls, JSValueConst self, JSAtom atom, JSValue* out) {
    if (g_overrides.empty()) return false;
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        for (const MethodDef& d : g_overrides) {
            if (d.cls != c || d.atom != atom) continue;
            JSValueConst data[2] = {d.fn, self};
            *out = JS_NewCFunctionData(ctx, callBound, 0, 0, 2, const_cast<JSValue*>(data));
            return true;
        }
    }
    return false;
}

bool extraAccessorGet(JSContext* ctx, Il2CppClass* cls, JSValueConst self, JSAtom atom, JSValue* out) {
    const AccessorDef* d = findAccessor(cls, atom);
    if (!d) return false;
    *out = JS_Call(ctx, d->get, self, 0, nullptr);
    return true;
}

int extraAccessorSet(JSContext* ctx, Il2CppClass* cls, JSValueConst self, JSAtom atom, JSValueConst value) {
    const AccessorDef* d = findAccessor(cls, atom);
    if (!d) return 0;
    if (JS_IsUndefined(d->set)) {
        const char* n = JS_AtomToCString(ctx, atom);
        JS_ThrowTypeError(ctx, "'%s' is read-only", n ? n : "?");
        if (n) JS_FreeCString(ctx, n);
        return -1;
    }
    JSValue r = JS_Call(ctx, d->set, self, 1, &value);
    if (JS_IsException(r)) return -1;
    JS_FreeValue(ctx, r);
    return 1;
}

bool hasExtraAccessor(Il2CppClass* cls, JSAtom atom) {
    return findAccessor(cls, atom) != nullptr;
}

bool hasExtraMethod(Il2CppClass* cls, JSAtom atom) {
    auto& a = il2cpp::api();
    for (Il2CppClass* c = cls; c && !g_methods.empty(); c = a.class_get_parent(c)) {
        for (const MethodDef& d : g_methods) {
            if (d.cls == c && d.atom == atom) return true;
        }
    }
    return false;
}

JSValue extraFieldGet(JSContext* ctx, Il2CppObject* obj, JSAtom atom) {
    Entry* e = find(obj);
    if (!e) return JS_UNDEFINED;
    return JS_GetProperty(ctx, e->fields, atom);
}

int extraFieldSet(JSContext* ctx, Il2CppObject* obj, JSAtom atom, JSValueConst value) {
    g_ctx = ctx;
    Entry* e = find(obj);
    if (!e) {
        if (++g_sinceSweep >= kSweepEvery) {
            g_sinceSweep = 0;
            sweep();
        }
        Entry fresh;
        fresh.weak = il2cpp::api().gchandle_new_weakref(obj, false);
        fresh.fields = JS_NewObjectProto(ctx, JS_NULL);
        e = &(g_entries[obj] = fresh);
    }
    if (JS_SetProperty(ctx, e->fields, atom, JS_DupValue(ctx, value)) < 0) return -1;
    return 1;
}

void installExtraFields(JSContext* ctx, JSValueConst bl) {
    g_ctx = ctx;
    JS_SetPropertyStr(ctx, bl, "defineField", JS_NewCFunction(ctx, js_defineField, "defineField", 2));
    JS_SetPropertyStr(ctx, bl, "defineMethod", JS_NewCFunction(ctx, js_defineMethod, "defineMethod", 3));
    JS_SetPropertyStr(ctx, bl, "defineProperty", JS_NewCFunction(ctx, js_defineProperty, "defineProperty", 4));
    JS_SetPropertyStr(ctx, bl, "addressOf", JS_NewCFunction(ctx, js_addressOf, "addressOf", 1));
    JS_SetPropertyStr(ctx, bl, "objectAt", JS_NewCFunction(ctx, js_objectAt, "objectAt", 1));
}

} // namespace bl::script

#endif
