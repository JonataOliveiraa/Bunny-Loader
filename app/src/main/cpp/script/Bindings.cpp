#include "script/ScriptEngine.h"
#include "script/Bridge.h"
#include "script/ExtraFields.h"
#include "script/Roots.h"
#include "script/WrapperMap.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "script/Invoke.h"
#include "script/Items.h"
#include "script/Npcs.h"
#include "script/Projectiles.h"
#include "script/Buffs.h"
#include "script/Files.h"
#include "script/Texture.h"
#include "script/Marshal.h"
#include "script/Ref.h"
#include "script/Members.h"
#include "script/Value.h"
#include "il2cpp/Types.h"
#include "runtime/TypeTables.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <cstdint>
#include <cstring>
#include <set>
#include <cctype>
#include <string>
#include <unordered_map>
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
    /**
     * O `this` e um OBJETO, mesmo quando chamado num struct: o metodo foi
     * declarado num tipo de referencia (GetType e Equals de System.Object,
     * ToString de ValueType). Metodo do proprio struct recebe os DADOS; esse
     * recebe a caixa, com cabecalho — dar os dados a ele faz o runtime ler o
     * primeiro campo como se fosse a classe.
     */
    bool thisIsObject;
};

// Declaradas adiante: o exotic da classe precisa delas antes de a secao de
// campos existir no arquivo. (makeGameArray/makeGameMethod vem de Bridge.h.)
JSValue readStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d);
void ensureStaticsReady(const Member& m);
int writeStaticField(JSContext* ctx, FieldInfo* f, const TypeDesc& d, JSValueConst value);

/**
 * Objeto do jogo segurado pelo JS.
 *
 * O coletor do IL2CPP (Boehm) varre a pilha e as raizes do jogo, nao a memoria
 * do QuickJS. Um objeto que so o mod segura — `Item.new()` guardado numa
 * variavel, um array guardado para depois — seria recolhido e o ponteiro
 * ficaria pendurado. A ancora (Roots: um slot numa tabela que o coletor
 * varre) diz a ele que alguem ainda usa o objeto; o finalizador do JS a solta.
 */
struct Pinned {
    void* ptr;
    uint32_t handle;
    // So em GameArray: o tipo do elemento, descoberto no primeiro acesso.
    // Perguntar ao IL2CPP a classe e o elemento a cada `arr[i]` custava mais
    // que ler o elemento (docs/PONTE-OTIMIZACAO.md, passo 4).
    const TypeDesc* elem = nullptr;
};

// Pinned soltos, reusados: um wrapper novo por acesso (elemento de array que o
// JS nao guarda, `self` de hook) era um new/delete a mais por objeto. Mesmo
// esquema do pool de StructRef (Value.cpp); so com o motor travado.
std::vector<Pinned*> g_freePinned;
constexpr size_t kPinnedPoolMax = 256;

Pinned* pin(void* p) {
    Pinned* r;
    if (g_freePinned.empty()) {
        r = new Pinned;
    } else {
        r = g_freePinned.back();
        g_freePinned.pop_back();
    }
    *r = Pinned{p, Roots::add(p), nullptr};
    return r;
}

void unpin(Pinned* p) {
    if (!p) return;
    Roots::remove(p->handle);
    if (g_freePinned.size() < kPinnedPoolMax) g_freePinned.push_back(p);
    else delete p;
}

/**
 * Um wrapper por objeto do jogo: `Main.player[0]` lido duas vezes devolve o
 * MESMO objeto JS. Economiza a alocacao e a ancora da segunda leitura em
 * diante, e faz `Main.player[0] === self` dar true, como no C#.
 *
 * O mapa NAO segura o wrapper (nao conta referencia): quem o mantem vivo e o
 * JS. Quando a ultima referencia cai, o QuickJS finaliza na hora — antes de
 * qualquer outro codigo rodar — e o finalizador tira a entrada; entao o mapa
 * nunca devolve um wrapper morto. A chave e estavel porque o coletor do
 * IL2CPP (Boehm) nao move objetos, e a ancora do wrapper impede que o
 * endereco seja recolhido e reusado enquanto a entrada existir.
 *
 * So com o motor travado (JsLock), como o resto da ponte.
 */
WrapperMap g_objectWrappers;   // GameObject
WrapperMap g_arrayWrappers;    // GameArray

/** Tira a entrada do wrapper que esta sendo finalizado — so se for ele. */
void forgetWrapper(WrapperMap& map, JSValue val, const Pinned* p) {
    if (!p || !p->ptr) return;
    JSValue* known = map.find(p->ptr);
    if (known && JS_VALUE_GET_PTR(*known) == JS_VALUE_GET_PTR(val)) map.erase(p->ptr);
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
/** Um valor como texto de log: objeto e array JS em JSON, o resto pelo toString. */
std::string logText(JSContext* ctx, JSValueConst v) {
    std::string out;
    const char* text = JS_ToCString(ctx, v);
    if (text) out = text;
    else { JS_FreeValue(ctx, JS_GetException(ctx)); out = "?"; }
    if (text) JS_FreeCString(ctx, text);
    if (JS_IsArray(v) || (JS_IsObject(v) && !JS_IsFunction(ctx, v) && out == "[object Object]")) {
        JSValue json = JS_JSONStringify(ctx, v, JS_UNDEFINED, JS_UNDEFINED);
        if (JS_IsException(json)) {
            JS_FreeValue(ctx, JS_GetException(ctx));   // ciclo, BigInt...: fica o toString
        } else if (const char* j = JS_ToCString(ctx, json)) {
            out = j;
            JS_FreeCString(ctx, j);
        }
        JS_FreeValue(ctx, json);
    }
    return out;
}

// bl.log(a, b, ...) — uma linha, os valores separados por espaco (como o
// console.log). Vai para o logcat e para logs/bunny.txt.
JSValue js_bl_log(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    std::string line;
    for (int i = 0; i < argc; ++i) {
        if (i) line += ' ';
        line += logText(ctx, argv[i]);
    }
    BL_INFO("[mod] %s", line.c_str());
    return JS_UNDEFINED;
}

// ============================ NativeClass ============================

JSValue nc_new(JSContext* ctx, JSValueConst self, int, JSValueConst*);
JSValue nc_makeGeneric(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv);

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
    if (m.field) {
        ensureStaticsReady(m);
        return readStaticField(ctx, m.field, *m.type);
    }
    if (m.getter) return invokeGetter(ctx, m.getter, nullptr);
    if (m.nested) {
        JSValue r = JS_NewObjectClass(ctx, g_nativeClassId);
        if (!JS_IsException(r)) JS_SetOpaque(r, m.nested);
        return r;
    }

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
    if (m.field) {
        ensureStaticsReady(m);
        return writeStaticField(ctx, m.field, *m.type, value);
    }
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
    JS_CFUNC_DEF("makeGeneric", 1, nc_makeGeneric),
};

/**
 * Campo estático: o IL2CPP só entrega CÓPIA do bloco de estáticos, nunca o
 * endereço. Para struct isso muda tudo — a vista tem de guardar o FieldInfo e
 * devolver o valor a cada escrita, senão `Main.screenPosition.X = 0` não faz
 * nada e o modder passa a tarde procurando o motivo.
 */
/**
 * O C# roda o construtor estatico de uma classe no primeiro acesso a um
 * estatico dela; o field_static_get_value do IL2CPP nao. Sem isto,
 * `AmmoID.Sets.IsArrow` lido antes de o jogo usar municao vinha null: o array
 * so nasce no construtor estatico. Uma vez por membro (o cache e perpetuo).
 */
void ensureStaticsReady(const Member& m) {
    if (m.classReady) return;
    m.classReady = true;
    auto& a = il2cpp::api();
    if (!a.runtime_class_init || !a.field_get_parent) return;
    if (Il2CppClass* owner = a.field_get_parent(m.field)) a.runtime_class_init(owner);
}

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
    // Referencia (objeto, array, texto): o IL2CPP grava o PROPRIO ponteiro
    // recebido (StaticSetValue nao desreferencia), nao o que ha nele. Passar o
    // buffer punha o endereco da pilha no estatico: `Main.recipe = x` lia
    // length 0 depois, e o jogo escrevia fora do array.
    if (!d.byValue) {
        a.field_static_set_value(f, *reinterpret_cast<void**>(buf.data()));
        return true;
    }
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
    // Campo que um mod pos na classe (bl.defineField): `item.ModItem`.
    if (isExtraField(cls, atom)) return extraFieldGet(ctx, o, atom);
    // Metodo que um mod pos na classe (bl.defineMethod): `player.GetModPlayer(X)`.
    JSValue method;
    if (extraMethodGet(ctx, cls, obj, atom, &method)) return method;
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
    if (isExtraField(cls, atom)) return extraFieldSet(ctx, o, atom, value);
    // Nome que a classe nao tem: RECUSA, como ja fazia o caminho do struct.
    // Antes isto virava uma propriedade JS comum no wrapper, entao um
    // `item.useTmie = 4` dava certo, nao mudava nada no jogo e nao dizia nada
    // — o tipo de erro que se procura por uma tarde inteira.
    JS_ThrowTypeError(ctx, "%s nao tem o campo %s",
                      il2cpp::api().class_get_name(cls), atomName(ctx, atom).c_str());
    return -1;
}

void no_finalizer(JSRuntime*, JSValue val) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(val, g_nativeObjectId));
    forgetWrapper(g_objectWrappers, val, p);
    unpin(p);
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
    auto& a = il2cpp::api();
    // Como o `new` do C#: o construtor estatico roda antes da primeira instancia.
    if (a.runtime_class_init) a.runtime_class_init(cls);
    Il2CppObject* obj = a.object_new(cls);
    if (!obj) return JS_ThrowInternalError(ctx, "object_new falhou");
    return makeNativeObject(ctx, obj);
}

/**
 * List.makeGeneric(Vector2) = List<Vector2>; Dictionary.makeGeneric(Int32,
 * Int32) = Dictionary<int, int>. Pelo caminho do proprio .NET:
 * Type.MakeGenericType sobre os System.Type das classes. So da certo para uma
 * combinacao que o jogo ja usa (o IL2CPP compilou o codigo dela) — para as
 * outras o runtime lanca, e a mensagem diz qual.
 */
JSValue nc_makeGeneric(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppClass* open = classOf(self);
    auto& a = il2cpp::api();
    if (!open) return JS_EXCEPTION;
    if (!a.type_get_object || !a.class_from_system_type) {
        return JS_ThrowInternalError(ctx, "makeGeneric: este runtime nao expoe a reflexao de tipos");
    }
    if (argc < 1) return JS_ThrowTypeError(ctx, "makeGeneric(Tipo, ...): passe os tipos");
    static Il2CppClass* typeCls = il2cpp::findClassQuiet("System", "Type");
    if (!typeCls) return JS_ThrowInternalError(ctx, "makeGeneric: System.Type nao encontrado");
    Il2CppArray* args = a.array_new(typeCls, static_cast<uintptr_t>(argc));
    for (int i = 0; i < argc; ++i) {
        Il2CppClass* c = classOf(argv[i]);
        if (!c) return JS_ThrowTypeError(ctx, "makeGeneric: argumento %d nao e uma classe", i);
        Il2CppObject* t = a.type_get_object(a.class_get_type(c));
        auto** slot = reinterpret_cast<Il2CppObject**>(arrayData(args)) + i;
        if (a.gc_wbarrier_set_field) {
            a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(args), reinterpret_cast<void**>(slot), t);
        } else {
            *slot = t;
        }
    }
    Il2CppObject* openType = a.type_get_object(a.class_get_type(open));
    const MethodInfo* make = a.class_get_method_from_name(a.object_get_class(openType), "MakeGenericType", 1);
    if (!make) return JS_ThrowInternalError(ctx, "makeGeneric: Type.MakeGenericType nao encontrado");
    void* params[1] = {args};
    Il2CppObject* exc = nullptr;
    Il2CppObject* closed = a.runtime_invoke(make, openType, params, &exc);
    if (exc || !closed) {
        return JS_ThrowTypeError(ctx, "makeGeneric: %s nao aceitou esses tipos", a.class_get_name(open));
    }
    Il2CppClass* cls = a.class_from_system_type(closed);
    if (!cls) return JS_ThrowInternalError(ctx, "makeGeneric: classe generica nao resolvida");
    JSValue v = JS_NewObjectClass(ctx, g_nativeClassId);
    if (!JS_IsException(v)) JS_SetOpaque(v, cls);
    return v;
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
            if (r->thisIsObject) {
                if (Il2CppObject* o = objectFromJS(v)) return o;
                if (Il2CppArray* arr = arrayFromJS(v)) return arr;
                // Vista de struct sem caixa: encaixota uma copia. O metodo e
                // de System.Object/ValueType e nao muda o struct.
                Il2CppClass* cls = nullptr;
                void* data = structDataOf(v, &cls, nullptr);
                auto& a = il2cpp::api();
                return data && cls && a.value_box ? a.value_box(cls, data) : nullptr;
            }
            if (void* s = structDataOf(v, nullptr, nullptr)) return s;
            if (Il2CppObject* o = objectFromJS(v)) return o;
            return arrayFromJS(v);
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

// NativeMethod.hook(callback[, { minType, on, field, whileIn }])
//
// Com `minType`, o hook so chama o JS quando o objeto `on` ('self', o padrao,
// ou o indice de um parametro) tem `field` (padrao 'type') >= minType. Ex.:
// NPC.AI so para NPC de mod — os do jogo nem entram no JS.
// Com `whileIn` (outro metodo, ja hookado), so chama o JS enquanto a thread
// esta dentro do hook dele.
JSValue nm_hook(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    auto* r = static_cast<MethodRef*>(JS_GetOpaque(self, g_nativeMethodId));
    if (!r || argc < 1) return JS_EXCEPTION;
    if (!JS_IsFunction(ctx, argv[0]))
        return JS_ThrowTypeError(ctx, "hook(callback): callback deve ser funcao");
    HookFilter filter;
    if (argc >= 2 && JS_IsObject(argv[1])) {
        JSValue gate = JS_GetPropertyStr(ctx, argv[1], "whileIn");
        if (!JS_IsUndefined(gate)) {
            auto* g = static_cast<MethodRef*>(JS_GetOpaque(gate, g_nativeMethodId));
            JS_FreeValue(ctx, gate);
            if (!g) return JS_ThrowTypeError(ctx, "hook: whileIn tem de ser um metodo do jogo");
            filter.whileIn = g->method;
        }
    }
    bool hasMin = false;
    if (argc >= 2 && JS_IsObject(argv[1])) {
        const JSAtom at = JS_NewAtom(ctx, "minType");
        hasMin = JS_HasProperty(ctx, argv[1], at) > 0;
        JS_FreeAtom(ctx, at);
    }
    if (hasMin) {
        JSValue min = JS_GetPropertyStr(ctx, argv[1], "minType");
        JSValue on = JS_GetPropertyStr(ctx, argv[1], "on");
        JSValue field = JS_GetPropertyStr(ctx, argv[1], "field");
        int32_t n = 0;
        const bool ok = JS_ToInt32(ctx, &n, min) == 0;
        filter.minType = n;
        filter.on = -1;
        if (JS_IsNumber(on)) {
            int32_t i = 0;
            JS_ToInt32(ctx, &i, on);
            filter.on = i;
        }
        if (JS_IsString(field)) {
            const char* s = JS_ToCString(ctx, field);
            if (s) { filter.field = s; JS_FreeCString(ctx, s); }
        }
        JS_FreeValue(ctx, min);
        JS_FreeValue(ctx, on);
        JS_FreeValue(ctx, field);
        if (!ok) return JS_EXCEPTION;
    }
    // installJsHook deixa a exceção posta, com o motivo exato (retorno struct,
    // argumentos demais, sem slot). Repetir aqui só apagaria a informação.
    if (!installJsHook(ctx, r->method, r->paramCount, r->isInstance, argv[0],
                       filter.on == -2 && !filter.whileIn ? nullptr : &filter))
        return JS_EXCEPTION;
    return JS_UNDEFINED;
}

const JSCFunctionListEntry nm_proto[] = {
    JS_CFUNC_DEF("hook", 2, nm_hook),
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

/** Tipo do elemento, guardado no wrapper do array depois da primeira vez. */
const TypeDesc& elemOf(JSContext* ctx, JSValueConst obj, Il2CppArray* arr) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(obj, g_gameArrayId));
    if (p && p->elem) return *p->elem;
    const TypeDesc& d = elemTypeOf(ctx, arr);
    if (p && d.size) p->elem = &d;   // describe() e perpetuo: o ponteiro vale sempre
    return d;
}

/**
 * O indice que o atomo representa, sem passar por texto. -1 = nao e indice.
 *
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

JSAtom lengthAtom(JSContext* ctx) {
    static JSAtom atom = JS_NewAtom(ctx, "length");   // referencia para sempre
    return atom;
}

/**
 * arr.cloneResized(n): um array NOVO do mesmo tipo, com n posicoes — o que
 * cabe vem de `arr`, o resto fica 0/null. O original nao muda; para trocar a
 * tabela do jogo, atribua: `Main.recipe = Main.recipe.cloneResized(n)`.
 */
JSValue ga_cloneResized(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppArray* arr = arrayFromJS(self);
    if (!arr) return JS_ThrowTypeError(ctx, "cloneResized: chame num array do jogo");
    int64_t n = -1;
    if (argc < 1 || JS_ToInt64(ctx, &n, argv[0]) < 0) return JS_EXCEPTION;
    if (n < 0 || n > 0x7fffffff) return JS_ThrowRangeError(ctx, "cloneResized: tamanho %lld invalido", static_cast<long long>(n));
    Il2CppArray* copy = runtime::TypeTables::resizedCopy(arr, static_cast<uintptr_t>(n));
    if (!copy) return JS_ThrowInternalError(ctx, "cloneResized: o jogo recusou a copia");
    return makeGameArray(ctx, copy);
}

JSValue ga_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppArray* arr = arrayFromJS(obj);
    if (!arr) return JS_UNDEFINED;
    const int64_t idx = indexOf(ctx, atom);
    if (idx < 0) {
        if (atom == lengthAtom(ctx)) return JS_NewInt64(ctx, static_cast<int64_t>(arr->length));
        // Uma funcao so, para sempre (o runtime JS vive ate o processo morrer).
        static JSAtom cloneAtom = JS_NewAtom(ctx, "cloneResized");
        static JSValue cloneFn = JS_NewCFunction(ctx, ga_cloneResized, "cloneResized", 1);
        if (atom == cloneAtom) return JS_DupValue(ctx, cloneFn);
        return JS_UNDEFINED;
    }
    if (static_cast<uint64_t>(idx) >= arr->length) {
        return JS_ThrowRangeError(ctx, "indice %lld fora de 0..%llu", static_cast<long long>(idx),
                                  static_cast<unsigned long long>(arr->length));
    }
    const TypeDesc& d = elemOf(ctx, obj, arr);
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
    if (!arr) return -1;
    const int64_t idx = indexOf(ctx, atom);
    if (idx < 0) {
        return JS_DefinePropertyValue(ctx, obj, atom, JS_DupValue(ctx, value),
                                      JS_PROP_C_W_E) < 0 ? -1 : true;
    }
    if (static_cast<uint64_t>(idx) >= arr->length) {
        JS_ThrowRangeError(ctx, "indice %lld fora de 0..%llu", static_cast<long long>(idx),
                           static_cast<unsigned long long>(arr->length));
        return -1;
    }
    const TypeDesc& d = elemOf(ctx, obj, arr);
    if (!d.size) return -1;
    return writeAt(ctx, reinterpret_cast<char*>(arrayData(arr)) +
                            static_cast<size_t>(idx) * strideOf(d),
                   d, value);
}

void ga_finalizer(JSRuntime*, JSValue val) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(val, g_gameArrayId));
    forgetWrapper(g_arrayWrappers, val, p);
    unpin(p);
}

const JSClassExoticMethods ga_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ga_exotic_get, ga_exotic_set,
};

} // namespace

// --- Exportado (Bridge.h): quem detém os JSClassID é este arquivo. ---

Il2CppClass* classFromJS(JSValueConst v) { return classOf(v); }

Il2CppObject* objectFromJS(JSValueConst v) {
    return objOf(v);
}

Il2CppArray* arrayFromJS(JSValueConst v) {
    if (auto* p = static_cast<Pinned*>(JS_GetOpaque(v, g_gameArrayId))) {
        return static_cast<Il2CppArray*>(p->ptr);
    }
    // Array que chegou como objeto comum (veio de um metodo que declara
    // devolver System.Array ou object, como Array.CreateInstance).
    Il2CppObject* o = objOf(v);
    return o && isArrayObject(o) ? reinterpret_cast<Il2CppArray*>(o) : nullptr;
}

bool isArrayObject(Il2CppObject* o) {
    auto& a = il2cpp::api();
    if (!o || !a.class_get_rank) return false;
    return a.class_get_rank(a.object_get_class(o)) > 0;
}

namespace {

/** O wrapper que ja existe para `ptr`, ou um novo, ancorado e registrado. */
JSValue wrapperFor(JSContext* ctx, WrapperMap& map, JSClassID cls, void* ptr) {
    if (ptr) {
        if (JSValue* known = map.find(ptr)) return JS_DupValue(ctx, *known);
    }
    JSValue v = JS_NewObjectClass(ctx, cls);
    if (JS_IsException(v)) return v;
    JS_SetOpaque(v, pin(ptr));
    if (ptr) map.insert(ptr, v);   // sem Dup: o mapa nao segura o wrapper
    return v;
}

} // namespace

JSValue makeNativeObject(JSContext* ctx, Il2CppObject* obj) {
    return wrapperFor(ctx, g_objectWrappers, g_nativeObjectId, obj);
}

JSValue makeGameArray(JSContext* ctx, Il2CppArray* arr) {
    return wrapperFor(ctx, g_arrayWrappers, g_gameArrayId, arr);
}

JSValue makeGameMethod(JSContext* ctx, const MethodInfo* m) {
    // Um GameMethod por metodo, criado uma vez: `obj['void Foo()']()` num laco
    // montava um objeto novo (malloc + objeto JS + method_get_flags) a cada
    // volta — 290 ns jogados fora por chamada (docs/PONTE-OTIMIZACAO.md,
    // passo 3). O cache segura a referencia para sempre, como os callbacks de
    // hook: o runtime JS vive ate o processo morrer (shutdown() nao e chamado).
    static std::unordered_map<const MethodInfo*, JSValue> cache;
    auto hit = cache.find(m);
    if (hit != cache.end()) return JS_DupValue(ctx, hit->second);

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
    auto& a = il2cpp::api();
    Il2CppClass* declaring = a.method_get_class(m);
    ref->thisIsObject = ref->isInstance && declaring && a.class_is_valuetype &&
                        !a.class_is_valuetype(declaring);
    JSValue v = JS_NewObjectClass(ctx, g_nativeMethodId);
    if (JS_IsException(v)) {
        std::free(ref);
        return v;
    }
    JS_SetOpaque(v, ref);
    cache.emplace(m, JS_DupValue(ctx, v));
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

    Il2CppClass* cls = il2cpp::findClassQuiet(*prefix, name);
    // Generico aberto: no metadado, `List` e ``List`1``. So nas duas imagens
    // principais, que e busca por hash; varrer todas a cada `Terraria.ID`
    // custaria caro.
    auto& a = il2cpp::api();
    for (int n = 1; !cls && n <= 4 && std::isupper(static_cast<unsigned char>(name[0])); ++n) {
        const std::string generic = name + "`" + std::to_string(n);
        for (const Il2CppImage* img : {a.gameImage, a.corlibImage}) {
            if (img && !cls) cls = a.class_from_name(img, prefix->c_str(), generic.c_str());
        }
    }
    if (cls) {
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

// data[0] = o nome da classe. Resolve e troca o acessor pelo valor.
JSValue globalClassGet(JSContext* ctx, JSValueConst thisVal, int, JSValueConst*, int, JSValueConst* data) {
    const char* name = JS_ToCString(ctx, data[0]);
    if (!name) return JS_EXCEPTION;
    Il2CppClass* cls = il2cpp::findClassQuiet("", name);
    JSValue v = JS_UNDEFINED;
    if (cls) {
        v = JS_NewObjectClass(ctx, g_nativeClassId);
        if (!JS_IsException(v)) JS_SetOpaque(v, cls);
    }
    JSAtom atom = JS_NewAtom(ctx, name);
    JS_FreeCString(ctx, name);
    JS_DefinePropertyValue(ctx, thisVal, atom, JS_DupValue(ctx, v), JS_PROP_C_W_E);
    JS_FreeAtom(ctx, atom);
    return v;
}

// `globalThis.X = ...` de quem chega depois (ajudantes, mods) vence a classe.
JSValue globalClassSet(JSContext* ctx, JSValueConst thisVal, int argc, JSValueConst* argv, int, JSValueConst* data) {
    const char* name = JS_ToCString(ctx, data[0]);
    if (!name) return JS_EXCEPTION;
    JSAtom atom = JS_NewAtom(ctx, name);
    JS_FreeCString(ctx, name);
    JS_DefinePropertyValue(ctx, thisVal, atom, JS_DupValue(ctx, argc > 0 ? argv[0] : JS_UNDEFINED),
                           JS_PROP_C_W_E);
    JS_FreeAtom(ctx, atom);
    return JS_UNDEFINED;
}

bool isPlainName(const char* n) {
    if (!n || !std::isalpha(static_cast<unsigned char>(n[0]))) return false;
    for (const char* p = n; *p; ++p) {
        if (!std::isalnum(static_cast<unsigned char>(*p)) && *p != '_') return false;
    }
    return true;
}

/**
 * As classes do jogo SEM namespace (GUIBuffs, GUIInstance, Main_Layout...)
 * como globais: `GUIBuffs['void Draw()'].hook(...)`. Preguicosas: o getter
 * acha a classe no primeiro acesso e vira valor. Nome que ja existe (Math,
 * bl, Terraria...) fica como esta; aninhada e gerada pelo compilador, fora.
 */
void installGlobalClasses(JSContext* ctx, JSValue global) {
    auto& a = il2cpp::api();
    const Il2CppImage* img = a.gameImage;
    if (!img) return;
    int published = 0;
    const size_t n = a.image_get_class_count(img);
    for (size_t i = 0; i < n; ++i) {
        Il2CppClass* c = a.image_get_class(img, i);
        if (!c) continue;
        const char* ns = a.class_get_namespace(c);
        if (ns && *ns) continue;
        if (a.class_get_declaring_type && a.class_get_declaring_type(c)) continue;
        const char* name = a.class_get_name(c);
        if (!isPlainName(name)) continue;
        JSAtom atom = JS_NewAtom(ctx, name);
        const int has = JS_HasProperty(ctx, global, atom);
        if (has == 0) {
            JSValue key = JS_NewString(ctx, name);
            JSValue get = JS_NewCFunctionData(ctx, globalClassGet, 0, 0, 1, &key);
            JSValue set = JS_NewCFunctionData(ctx, globalClassSet, 1, 0, 1, &key);
            JS_FreeValue(ctx, key);
            if (JS_DefinePropertyGetSet(ctx, global, atom, get, set, JS_PROP_CONFIGURABLE) >= 0) ++published;
        }
        JS_FreeAtom(ctx, atom);
    }
    BL_INFO("namespaces: %d classe(s) sem namespace publicadas como globais", published);
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
    installGlobalClasses(ctx, global);
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
    installExtraFields(ctx, bl);
    installItemsApi(ctx, bl);
    installProjectilesApi(ctx, bl);
    installBuffsApi(ctx, bl);
    installFilesApi(ctx, bl);
    installNpcsApi(ctx, bl);
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
    installRefClass(ctx, global);   // ref/out (Ref.h)

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
