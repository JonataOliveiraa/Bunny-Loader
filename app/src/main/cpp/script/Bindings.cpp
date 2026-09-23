#include "script/ScriptEngine.h"
#include "script/Bridge.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "script/Invoke.h"
#include "script/Items.h"
#include "script/Texture.h"
#include "script/Marshal.h"
#include "script/Members.h"
#include "script/Value.h"
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <cstdint>
#include <cstring>
#include <set>
#include <string>
#include <vector>
#include <cstdlib>
#endif

// Ponte JavaScript <-> IL2CPP: os objetos que o mod enxerga.
//
//   GameClass    Terraria.Item            classe; campo/propriedade estatica,
//                                         metodo por assinatura, .new()
//   GameObject   item                     instancia; campo/propriedade, metodo
//   GameMethod   Item['void SetDefaults'] chamavel e hookavel
//   GameArray    Main.player              indice e .length
//   GameStruct   item.position            struct por valor (ver Value.cpp)
//   Namespace    Terraria, Microsoft      arvore preguicosa ate achar a classe
//
// Como LER e ESCREVER cada tipo fica em Value.cpp, num lugar so. Este arquivo
// decide QUEM e o alvo; aquele decide o que ha naquele endereco.

namespace bl::script {

#if BL_HAVE_QUICKJS

namespace {

JSClassID g_nativeClassId;
JSClassID g_nativeObjectId;
JSClassID g_nativeMethodId;

// NativeMethod guarda isto (heap; liberado no finalizador).
struct MethodRef {
    const MethodInfo* method;
    int paramCount;
    bool isInstance;
};

// Declaradas adiante: o exotic da classe precisa delas antes de a secao de
// campos existir no arquivo. (makeGameArray/makeGameMethod vem de Bridge.h.)
JSValue readStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d);
int writeStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d, JSValueConst value);

/**
 * Objeto do jogo segurado pelo JS.
 *
 * O coletor do IL2CPP (Boehm) varre a pilha e as raizes do jogo, nao a memoria
 * do QuickJS. Um objeto que so o mod segura — `Item.new()` guardado numa
 * variavel, um array guardado para depois — seria recolhido e o ponteiro
 * ficaria pendurado. O gchandle e a raiz que diz ao coletor que alguem ainda
 * usa o objeto; o finalizador do JS o solta.
 */
struct Pinned {
    void* ptr;
    uint32_t handle;
};

Pinned* pin(void* p) {
    auto& a = il2cpp::api();
    uint32_t h = (p && a.gchandle_new) ? a.gchandle_new(static_cast<Il2CppObject*>(p), false) : 0;
    return new Pinned{p, h};
}

void unpin(Pinned* p) {
    if (!p) return;
    if (p->handle && il2cpp::api().gchandle_free) il2cpp::api().gchandle_free(p->handle);
    delete p;
}

Il2CppClass* classOf(JSValueConst v) {
    return static_cast<Il2CppClass*>(JS_GetOpaque(v, g_nativeClassId));
}
Il2CppObject* objOf(JSValueConst v) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(v, g_nativeObjectId));
    return p ? static_cast<Il2CppObject*>(p->ptr) : nullptr;
}

/** Propriedade C#: get_<nome>() em `self` (nullptr = estatica). */
JSValue invokeGetter(JSContext* ctx, const MethodInfo* g, void* self) {
    return invokeMethod(ctx, g, self, 0, nullptr);
}

/** Propriedade C#: set_<nome>(v). @return 1 ok, -1 com excecao posta. */
int invokeSetter(JSContext* ctx, const MethodInfo* sm, void* self, JSValueConst value) {
    JSValue r = invokeMethod(ctx, sm, self, 1, &value);
    if (JS_IsException(r)) return -1;
    JS_FreeValue(ctx, r);
    return true;
}

/** Nome sem nada do jogo por tras: vira propriedade JS comum no objeto. */
int defineJsProperty(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst value) {
    return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value), JS_PROP_C_W_E) < 0
               ? -1 : true;
}

/** "a | b | c" — so no caminho de erro. */
std::string overloadList(Il2CppClass* cls, const std::string& name) {
    auto overloads = il2cpp::listOverloads(cls, name);
    std::string msg;
    for (size_t i = 0; i < overloads.size(); ++i) {
        if (i) msg += " | ";
        msg += overloads[i];
    }
    return msg;
}

// --- bl.log ---
JSValue js_bl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    for (int i = 0; i < argc; ++i) {
        const char* text = JS_ToCString(ctx, argv[i]);
        BL_INFO("[mod] %s", text ? text : "undefined");
        if (text) JS_FreeCString(ctx, text);
    }
    return JS_UNDEFINED;
}

// ============================ NativeClass ============================

JSValue nc_new(JSContext* ctx, JSValueConst self, int, JSValueConst*);

JSValue js_NativeClass(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "NativeClass(namespace, nome)");
    const char* ns = JS_ToCString(ctx, argv[0]);
    const char* name = JS_ToCString(ctx, argv[1]);
    JSValue r;
    if (!ns || !name) r = JS_EXCEPTION;
    else {
        Il2CppClass* cls = il2cpp::findClass({ns, name, {}});
        if (!cls) r = JS_ThrowTypeError(ctx, "classe '%s.%s' nao encontrada", ns, name);
        else {
            r = JS_NewObjectClass(ctx, g_nativeClassId);
            if (!JS_IsException(r)) JS_SetOpaque(r, cls);
        }
    }
    if (ns) JS_FreeCString(ctx, ns);
    if (name) JS_FreeCString(ctx, name);
    return r;
}

/**
 * Acesso por propriedade e por ASSINATURA no NativeClass.
 *
 *   Item['void SetDefaults(int Type, ItemVariant variant)']  -> NativeMethod
 *   Item.SetDefaults                                          -> NativeMethod (se único)
 *   Player.defaultItemGrabRange                               -> valor do campo estático
 *
 * Substitui o `.method(nome, nParams)`, que casava por ARIDADE e por isso não
 * desambiguava overloads — o `Item.NewItem` tem quatro com 9 parâmetros.
 * Quando o nome puro é ambíguo NÃO escolhemos um: erramos, listando os
 * overloads. Escolher em silêncio foi o bug que deu exceção a cada frame.
 */
JSValue nc_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppClass* cls = classOf(obj);
    if (!cls) return JS_UNDEFINED;
    const Member& m = member(ctx, cls, atom, Space::Static, g_nativeClassId);

    // O que é nosso (`new`) vem do protótipo. Sem isto o exotic engoliria a
    // API inteira.
    if (m.proto) return protoGet(ctx, g_nativeClassId, atom);
    if (m.method) return makeGameMethod(ctx, m.method);
    // Campo estático, pelo tipo declarado (array vira GameArray, float vira
    // número, string vira string — antes tudo saía como int64).
    if (m.field) return readStaticField(ctx, m.field, *m.type);
    if (m.getter) return invokeGetter(ctx, m.getter, nullptr);

    // Daqui para baixo é só erro: pode ser lento, recalcula o que precisar.
    if (m.signature) {
        std::string name = atomName(ctx, atom);
        il2cpp::Signature sig = il2cpp::parseSignature(name);
        if (!sig.valid) return JS_ThrowTypeError(ctx, "assinatura invalida: '%s'", name.c_str());
        bool ambiguous = false;
        il2cpp::findMethodBySignature(cls, sig, &ambiguous);
        std::string msg = ambiguous ? "assinatura ambigua: '" : "metodo nao encontrado: '";
        msg += name + "'";
        std::string list = overloadList(cls, sig.name);
        if (!list.empty()) msg += ". Existem: " + list;
        return JS_ThrowTypeError(ctx, "%s", msg.c_str());
    }
    if (m.overloads > 1) {
        std::string name = atomName(ctx, atom);
        return JS_ThrowTypeError(ctx, "'%s' tem %d overloads; use a assinatura. Existem: %s",
                                 name.c_str(), m.overloads, overloadList(cls, name).c_str());
    }
    return JS_UNDEFINED;
}

int nc_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppClass* cls = classOf(obj);
    if (!cls) return -1;
    const Member& m = member(ctx, cls, atom, Space::Static, g_nativeClassId);
    if (m.field) return writeStaticField(ctx, m.field, *m.type, value);
    if (m.setter) return invokeSetter(ctx, m.setter, nullptr, value);
    return defineJsProperty(ctx, obj, atom, value);
}

const JSClassExoticMethods nc_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr,
    nc_exotic_get, nc_exotic_set,
};

// Tudo que estava aqui — getStaticInt, setStaticFloat, method(nome, aridade) —
// saiu: o acesso por propriedade e por assinatura faz o mesmo sem o modder
// precisar dizer o tipo nem contar parâmetros. O que fica no protótipo SOMBREIA
// nomes reais da classe do jogo, então quanto menos, melhor.
const JSCFunctionListEntry nc_proto[] = {
    JS_CFUNC_DEF("new", 0, nc_new),
};

/**
 * Campo estático: o IL2CPP só entrega CÓPIA do bloco de estáticos, nunca o
 * endereço. Para struct isso muda tudo — a vista tem de guardar o FieldInfo e
 * devolver o valor a cada escrita, senão `Main.screenPosition.X = 0` não faz
 * nada e o modder passa a tarde procurando o motivo.
 */
JSValue readStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d) {
    auto& a = il2cpp::api();
    if (d.prim == Prim::Struct) return makeStaticStruct(ctx, d.cls, f, d.size);
    uint8_t buf[16] = {0};  // primitivo ou ponteiro: no máximo 8 bytes
    a.field_static_get_value(f, buf);
    return readAt(ctx, buf, d, JS_UNDEFINED);
}

int writeStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d, JSValueConst value) {
    auto& a = il2cpp::api();
    std::vector<uint8_t> buf(d.size < sizeof(void*) ? sizeof(void*) : d.size, 0);
    // Lê antes: trocar um struct inteiro copia só os bytes dele, e o resto do
    // buffer iria por cima do valor atual.
    a.field_static_get_value(f, buf.data());
    int ok = writeAt(ctx, buf.data(), d, value);
    if (ok < 0) return ok;
    a.field_static_set_value(f, buf.data());
    return true;
}

// ============================ NativeObject ============================

/**
 * Campos de instância como propriedade: `item.useTime`, `item.useTime = 4`.
 *
 * O TIPO do campo decide como ler e escrever — o modder não tem como dizer
 * isso numa atribuição, e ler float como int devolve lixo. A interpretação
 * mora em Value.cpp; o que o nome É (campo, propriedade, método) vem do cache
 * de Members.cpp. `obj` é o dono da memória: quem receber uma vista de struct
 * (`item.position`) precisa dele vivo.
 */
JSValue no_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppObject* o = objOf(obj);
    if (!o) return JS_UNDEFINED;
    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    const Member& m = member(ctx, cls, atom, Space::Instance, g_nativeObjectId);

    if (m.proto) return protoGet(ctx, g_nativeObjectId, atom);
    if (m.field) return readAt(ctx, reinterpret_cast<char*>(o) + m.offset, *m.type, obj);
    if (m.method) return makeGameMethod(ctx, m.method);
    if (m.getter) return invokeGetter(ctx, m.getter, o);
    if (m.signature) {
        return JS_ThrowTypeError(ctx, "metodo nao encontrado: '%s'", atomName(ctx, atom).c_str());
    }
    return JS_UNDEFINED;
}

int no_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppObject* o = objOf(obj);
    if (!o) return -1;
    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    const Member& m = member(ctx, cls, atom, Space::Instance, g_nativeObjectId);
    if (m.field) return writeAt(ctx, reinterpret_cast<char*>(o) + m.offset, *m.type, value);
    if (m.setter) return invokeSetter(ctx, m.setter, o, value);
    // Nome que a classe nao tem: RECUSA, como ja fazia o caminho do struct.
    // Antes isto virava uma propriedade JS comum no wrapper, entao um
    // `item.useTmie = 4` dava certo, nao mudava nada no jogo e nao dizia nada
    // — o tipo de erro que se procura por uma tarde inteira.
    JS_ThrowTypeError(ctx, "%s nao tem o campo %s",
                      il2cpp::api().class_get_name(cls), atomName(ctx, atom).c_str());
    return -1;
}

void no_finalizer(JSRuntime*, JSValue val) {
    unpin(static_cast<Pinned*>(JS_GetOpaque(val, g_nativeObjectId)));
}

const JSClassExoticMethods no_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr,
    no_exotic_get, no_exotic_set,
};

/** `bl.log(item)` mostrando a classe, em vez de [object Object]. */
JSValue no_toString(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppObject* o = objOf(self);
    if (!o) return JS_NewString(ctx, "[GameObject]");
    const char* n = il2cpp::api().class_get_name(il2cpp::api().object_get_class(o));
    return JS_NewString(ctx, (std::string("[") + (n ? n : "?") + "]").c_str());
}

const JSCFunctionListEntry no_proto[] = {
    JS_CFUNC_DEF("toString", 0, no_toString),
};

// NativeClass.new(): cria uma instancia sem chamar o construtor (memoria
// zerada). Para inicializar de verdade, o mod deve chamar .ctor() depois
// (quando NativeMethod existir).
JSValue nc_new(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppClass* cls = classOf(self);
    if (!cls) return JS_EXCEPTION;
    Il2CppObject* obj = il2cpp::api().object_new(cls);
    if (!obj) return JS_ThrowInternalError(ctx, "object_new falhou");
    return makeNativeObject(ctx, obj);
}

// ============================ NativeMethod ============================

void nm_finalizer(JSRuntime*, JSValue val) {
    if (auto* r = static_cast<MethodRef*>(JS_GetOpaque(val, g_nativeMethodId)))
        std::free(r);
}

/**
 * Chamar o método do jogo, como função JS.
 *
 *   Terraria.Main['void NewText(string text)']('oi');     // estático
 *   item['void SetDefaults(int Type)'](98);               // de instância
 *
 * De onde sai a instância, nesta ordem:
 *   1. `this` — é o caso natural de `obj.Metodo(args)`;
 *   2. o primeiro argumento, se for objeto do jogo — é a forma do
 *      `original(self, ...)` do callback de hook.
 *
 * Exceção do lado do jogo NÃO é ignorada: vira exceção JS. Os devs do TL Pro
 * avisam que engolir isso derruba o jogo quando um invariante falha dentro de
 * um hook.
 */
JSValue gm_call(JSContext* ctx, JSValueConst func, JSValueConst thisVal,
                int argc, JSValueConst* argv, int) {
    auto* r = static_cast<MethodRef*>(JS_GetOpaque(func, g_nativeMethodId));
    if (!r) return JS_ThrowTypeError(ctx, "metodo invalido");

    // STRUCT: o runtime_invoke quer o ponteiro para os DADOS, não para o
    // objeto que os encaixota. Passar a caixa faz o método ler o cabeçalho
    // como se fosse o primeiro campo — sem erro, com lixo. `structDataOf`
    // resolve tanto um GameStruct quanto um objeto encaixotado.
    void* thisPtr = nullptr;
    if (r->isInstance) {
        auto self = [&](JSValueConst v) -> void* {
            if (void* s = structDataOf(v, nullptr, nullptr)) return s;
            return objectFromJS(v);
        };
        thisPtr = self(thisVal);
        if (!thisPtr && argc > 0) {
            thisPtr = self(argv[0]);
            if (thisPtr) { ++argv; --argc; }
        }
        if (!thisPtr) {
            return JS_ThrowTypeError(
                ctx, "'%s' e metodo de instancia: chame obj.Metodo(...) ou passe o objeto como 1o argumento",
                il2cpp::api().method_get_name(r->method));
        }
    }

    if (argc < r->paramCount) {
        return JS_ThrowTypeError(ctx, "'%s' espera %d argumento(s), recebeu %d",
                                 il2cpp::api().method_get_name(r->method),
                                 r->paramCount, argc);
    }

    return invokeMethod(ctx, r->method, thisPtr, argc, argv);
}

// NativeMethod.hook(callback)
JSValue nm_hook(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    auto* r = static_cast<MethodRef*>(JS_GetOpaque(self, g_nativeMethodId));
    if (!r || argc < 1) return JS_EXCEPTION;
    if (!JS_IsFunction(ctx, argv[0]))
        return JS_ThrowTypeError(ctx, "hook(callback): callback deve ser funcao");
    // installJsHook deixa a exceção posta, com o motivo exato (retorno struct,
    // argumentos demais, sem slot). Repetir aqui só apagaria a informação.
    if (!installJsHook(ctx, r->method, r->paramCount, r->isInstance, argv[0]))
        return JS_EXCEPTION;
    return JS_UNDEFINED;
}

const JSCFunctionListEntry nm_proto[] = {
    JS_CFUNC_DEF("hook", 1, nm_hook),
};


// ============================ GameArray ============================
//
// `Main.player[Main.myPlayer]`, `arr.length`. Sem isto metade dos mods
// clássicos de Terraria é impossível: pegar o jogador, varrer inimigos, mexer
// no inventário — tudo passa por array.
//
// O índice vira offset com o TAMANHO DO ELEMENTO, que difere: um Player[]
// guarda ponteiros (8 B); um Vector2[] guarda os structs em linha. Errar isso
// anda na memória errada sem dar erro.

JSClassID g_gameArrayId;

/** Tipo do elemento. É ele que dá o passo: ponteiro (8 B) ou struct em linha. */
const TypeDesc& elemTypeOf(JSContext* ctx, Il2CppArray* arr) {
    static const TypeDesc kUnknown = [] { TypeDesc d; d.size = 0; return d; }();
    auto& a = il2cpp::api();
    if (!arr || !a.class_get_element_class || !a.class_get_type) {
        JS_ThrowInternalError(ctx, "array: API de tipo de elemento ausente");
        return kUnknown;
    }
    Il2CppClass* arrCls = a.object_get_class(reinterpret_cast<Il2CppObject*>(arr));
    Il2CppClass* elem = a.class_get_element_class(arrCls);
    if (!elem) {
        JS_ThrowInternalError(ctx, "array: tipo do elemento desconhecido");
        return kUnknown;
    }
    return describe(a.class_get_type(elem));
}

size_t strideOf(const TypeDesc& d) { return d.byValue ? d.size : sizeof(void*); }

JSValue ga_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppArray* arr = arrayFromJS(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!arr || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    if (name == "length") return JS_NewInt64(ctx, static_cast<int64_t>(arr->length));

    // Índice numérico?
    char* end = nullptr;
    long idx = std::strtol(name.c_str(), &end, 10);
    if (!end || *end != '\0' || name.empty()) return JS_UNDEFINED;
    if (idx < 0 || static_cast<uintptr_t>(idx) >= arr->length) {
        return JS_ThrowRangeError(ctx, "indice %ld fora de 0..%llu", idx,
                                  static_cast<unsigned long long>(arr->length));
    }

    const TypeDesc& d = elemTypeOf(ctx, arr);
    if (!d.size) return JS_EXCEPTION;
    // O dono é o próprio array: um elemento struct sai como VISTA para dentro
    // dele, então `Main.rain[3].position.X = 0` altera o jogo de verdade.
    return readAt(ctx, reinterpret_cast<char*>(arrayData(arr)) +
                           static_cast<size_t>(idx) * strideOf(d),
                  d, obj);
}

int ga_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppArray* arr = arrayFromJS(obj);
    const char* key = JS_AtomToCString(ctx, atom);
    if (!arr || !key) { if (key) JS_FreeCString(ctx, key); return -1; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    char* end = nullptr;
    long idx = std::strtol(name.c_str(), &end, 10);
    if (!end || *end != '\0' || name.empty()) {
        return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value),
                                      JS_PROP_C_W_E) < 0 ? -1 : true;
    }
    if (idx < 0 || static_cast<uintptr_t>(idx) >= arr->length) {
        JS_ThrowRangeError(ctx, "indice %ld fora de 0..%llu", idx,
                           static_cast<unsigned long long>(arr->length));
        return -1;
    }

    const TypeDesc& d = elemTypeOf(ctx, arr);
    if (!d.size) return -1;
    return writeAt(ctx, reinterpret_cast<char*>(arrayData(arr)) +
                            static_cast<size_t>(idx) * strideOf(d),
                   d, value);
}

void ga_finalizer(JSRuntime*, JSValue val) {
    unpin(static_cast<Pinned*>(JS_GetOpaque(val, g_gameArrayId)));
}

const JSClassExoticMethods ga_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ga_exotic_get, ga_exotic_set,
};

} // namespace

// --- Exportado (Bridge.h): quem detém os JSClassID é este arquivo. ---

Il2CppObject* objectFromJS(JSValueConst v) {
    return objOf(v);
}

Il2CppArray* arrayFromJS(JSValueConst v) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(v, g_gameArrayId));
    return p ? static_cast<Il2CppArray*>(p->ptr) : nullptr;
}

JSValue makeNativeObject(JSContext* ctx, Il2CppObject* obj) {
    JSValue o = JS_NewObjectClass(ctx, g_nativeObjectId);
    if (!JS_IsException(o)) JS_SetOpaque(o, pin(obj));
    return o;
}

JSValue makeGameArray(JSContext* ctx, Il2CppArray* arr) {
    JSValue v = JS_NewObjectClass(ctx, g_gameArrayId);
    if (!JS_IsException(v)) JS_SetOpaque(v, pin(arr));
    return v;
}

JSValue makeGameMethod(JSContext* ctx, const MethodInfo* m) {
    // CUIDADO: il2cpp_method_get_flags DEVOLVE as flags e escreve as de
    // IMPLEMENTACAO no parâmetro de saída. Ler o parâmetro (o que este código
    // fazia) dava isInstance errado para todo método estático.
    uint32_t iflags = 0;
    uint32_t flags = il2cpp::api().method_get_flags(m, &iflags);
    auto* ref = static_cast<MethodRef*>(std::malloc(sizeof(MethodRef)));
    if (!ref) return JS_ThrowOutOfMemory(ctx);
    ref->method = m;
    ref->paramCount = static_cast<int>(il2cpp::api().method_get_param_count(m));
    ref->isInstance = !(flags & 0x0010);  // METHOD_ATTRIBUTE_STATIC
    JSValue v = JS_NewObjectClass(ctx, g_nativeMethodId);
    if (JS_IsException(v)) std::free(ref);
    else JS_SetOpaque(v, ref);
    return v;
}

// ============================ Namespace ============================
//
// `Terraria.Item`, `Terraria.ID.ItemID`. Cada objeto guarda um PREFIXO; o
// acesso a uma propriedade tenta primeiro resolver "<prefixo>.<nome>" como
// classe e, se não for, devolve outro Namespace com o prefixo estendido.
//
// Preguiçoso de propósito: o IL2CPP não tem API de "existe este namespace?",
// e materializar a árvore inteira no boot custaria varrer dezenas de milhares
// de classes. Se o caminho não levar a lugar nenhum, o erro aparece no acesso
// final, com o caminho completo.

JSClassID g_namespaceId;

JSValue makeNamespace(JSContext* ctx, const std::string& prefix) {
    JSValue v = JS_NewObjectClass(ctx, g_namespaceId);
    if (JS_IsException(v)) return v;
    auto* p = new std::string(prefix);
    JS_SetOpaque(v, p);
    return v;
}

void ns_finalizer(JSRuntime*, JSValue val) {
    delete static_cast<std::string*>(JS_GetOpaque(val, g_namespaceId));
}

JSValue ns_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    auto* prefix = static_cast<std::string*>(JS_GetOpaque(obj, g_namespaceId));
    const char* key = JS_AtomToCString(ctx, atom);
    if (!prefix || !key) { if (key) JS_FreeCString(ctx, key); return JS_UNDEFINED; }
    std::string name(key);
    JS_FreeCString(ctx, key);

    // Coisas que o motor consulta em qualquer objeto. Sem isto, um
    // `console.log(Terraria)` viraria um Namespace chamado "toString".
    if (name == "toString" || name == "valueOf" || name == "constructor" ||
        name == "then" || name == "length" || name.rfind("Symbol.", 0) == 0) {
        return JS_UNDEFINED;
    }

    if (Il2CppClass* cls = il2cpp::findClassQuiet(*prefix, name)) {
        JSValue v = JS_NewObjectClass(ctx, g_nativeClassId);
        if (!JS_IsException(v)) JS_SetOpaque(v, cls);
        return v;
    }
    return makeNamespace(ctx, prefix->empty() ? name : *prefix + "." + name);
}

const JSClassExoticMethods ns_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ns_exotic_get, nullptr,
};

/** bl.classOf(ns, nome) — quando a árvore não ajuda. */
JSValue js_loadTexture(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return loadTexture(ctx, argc, argv);
}

JSValue js_classOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "bl.classOf(namespace, nome)");
    return js_NativeClass(ctx, JS_UNDEFINED, argc, argv);
}

/**
 * Publica um global por namespace RAIZ encontrado nas imagens do jogo.
 *
 * Varre as classes uma vez, no boot, só para saber quais raízes existem
 * (Terraria, System, Microsoft, ReLogic...). É o que permite escrever
 * `Terraria.Item` sem o mod declarar nada.
 */
void installNamespaceRoots(JSContext* ctx, JSValue global) {
    auto& a = il2cpp::api();
    if (!a.image_get_class_count || !a.image_get_class || !a.class_get_namespace) {
        BL_WARN("namespaces: API de enumeracao ausente; use bl.classOf()");
        return;
    }
    std::set<std::string> roots;
    for (const Il2CppImage* img : {a.gameImage, a.corlibImage}) {
        if (!img) continue;
        size_t n = a.image_get_class_count(img);
        for (size_t i = 0; i < n; ++i) {
            Il2CppClass* c = a.image_get_class(img, i);
            if (!c) continue;
            const char* ns = a.class_get_namespace(c);
            if (!ns || !*ns) continue;
            const char* dot = std::strchr(ns, '.');
            roots.insert(dot ? std::string(ns, dot - ns) : std::string(ns));
        }
    }
    for (const auto& r : roots) {
        JS_SetPropertyStr(ctx, global, r.c_str(), makeNamespace(ctx, r));
    }
    BL_INFO("namespaces: %zu raizes publicadas", roots.size());
}

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSRuntime* rt = JS_GetRuntime(ctx);
    JSValue global = JS_GetGlobalObject(ctx);

    // `bl` de Bunny Loader. Era `tl`, herdado de espelhar a API do TL Pro —
    // nome de outro produto na API pública do nosso não faz sentido.
    JSValue bl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, bl, "log", JS_NewCFunction(ctx, js_bl_log, "log", 1));
    JS_SetPropertyStr(ctx, bl, "classOf", JS_NewCFunction(ctx, js_classOf, "classOf", 2));
    JS_SetPropertyStr(ctx, bl, "loadTexture",
                      JS_NewCFunction(ctx, js_loadTexture, "loadTexture", 1));
    installItemsApi(ctx, bl);
    JS_SetPropertyStr(ctx, global, "bl", bl);

    // NativeClass
    JS_NewClassID(rt, &g_nativeClassId);
    static const JSClassDef ncDef = {
        "GameClass", nullptr, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&nc_exotic),
    };
    JS_NewClass(rt, g_nativeClassId, &ncDef);
    JSValue ncProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, ncProto, nc_proto, sizeof(nc_proto)/sizeof(nc_proto[0]));
    JS_SetClassProto(ctx, g_nativeClassId, ncProto);


    // NativeObject (sem constructor JS; criado por NativeClass.new)
    JS_NewClassID(rt, &g_nativeObjectId);
    static const JSClassDef noDef = {
        "GameObject", no_finalizer, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&no_exotic),
    };
    JS_NewClass(rt, g_nativeObjectId, &noDef);
    JSValue noProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, noProto, no_proto, sizeof(no_proto)/sizeof(no_proto[0]));
    JS_SetClassProto(ctx, g_nativeObjectId, noProto);

    // NativeMethod (criado por NativeClass.method; tem finalizador)
    JS_NewClassID(rt, &g_nativeMethodId);
    static const JSClassDef nmDef = { "GameMethod", nm_finalizer, nullptr, gm_call, nullptr };
    JS_NewClass(rt, g_nativeMethodId, &nmDef);
    JSValue nmProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, nmProto, nm_proto, sizeof(nm_proto)/sizeof(nm_proto[0]));
    JS_SetClassProto(ctx, g_nativeMethodId, nmProto);

    // GameArray
    JS_NewClassID(rt, &g_gameArrayId);
    static const JSClassDef gaDef = {
        "GameArray", ga_finalizer, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&ga_exotic),
    };
    JS_NewClass(rt, g_gameArrayId, &gaDef);
    JS_SetClassProto(ctx, g_gameArrayId, JS_NewObject(ctx));

    // GameStruct (Value.cpp) — precisa existir antes de qualquer leitura de
    // campo, que é de onde saem as vistas de struct.
    installStructClass(ctx);

    // Namespace (arvore Terraria.*) — depois do g_nativeClassId existir.
    JS_NewClassID(rt, &g_namespaceId);
    static const JSClassDef nsDef = {
        "Namespace", ns_finalizer, nullptr, nullptr,
        const_cast<JSClassExoticMethods*>(&ns_exotic),
    };
    JS_NewClass(rt, g_namespaceId, &nsDef);
    JS_SetClassProto(ctx, g_namespaceId, JS_NewObject(ctx));
    installNamespaceRoots(ctx, global);

    JS_FreeValue(ctx, global);
    BL_INFO("bindings instalados (bl.log/classOf, arvore de namespaces)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
