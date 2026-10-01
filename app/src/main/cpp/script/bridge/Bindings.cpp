#include "script/bridge/ScriptEngine.h"
#include "script/bridge/Bridge.h"
#include "script/bridge/ExtraFields.h"
#include "script/bridge/Roots.h"
#include "script/bridge/WrapperMap.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "script/bridge/Invoke.h"
#include "script/api/Items.h"
#include "script/api/Npcs.h"
#include "script/api/Projectiles.h"
#include "script/api/Buffs.h"
#include "script/api/Tiles.h"
#include "script/api/Files.h"
#include "script/api/Texture.h"
#include "script/api/Sounds.h"
#include "script/api/Console.h"
#include "script/bridge/Marshal.h"
#include "script/bridge/Ref.h"
#include "script/bridge/Members.h"
#include "script/bridge/Value.h"
#include "il2cpp/Types.h"
#include "content/common/TypeTables.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <set>
#include <time.h>
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
    JSValue owner;  // wrapper de onde o metodo foi lido; mantem a instancia viva
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
    // que ler o elemento (docs/historico/PONTE-OTIMIZACAO.md, passo 4).
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
JSValue nc_newArray(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv);
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
/**
 * Um nome que a classe do jogo nao tem, mas o tModLoader tem
 * (`DustID.PinkFairy`): a tabela `__blExtraStatics` do ModHelpers.js, por
 * "Namespace.Classe". So no caminho do "nao achou".
 */
bool extraStatic(JSContext* ctx, Il2CppClass* cls, JSAtom atom, JSValue* out) {
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue extras = JS_GetPropertyStr(ctx, global, "__blExtraStatics");
    JS_FreeValue(ctx, global);
    bool found = false;
    if (JS_IsObject(extras)) {
        JSValue table = JS_GetPropertyStr(ctx, extras, className(cls).c_str());
        if (JS_IsObject(table) && JS_HasProperty(ctx, table, atom) > 0) {
            found = true;
            if (out) *out = JS_GetProperty(ctx, table, atom);
        }
        JS_FreeValue(ctx, table);
    }
    JS_FreeValue(ctx, extras);
    return found;
}

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
    if (m.signature) return missingSignature(ctx, cls, atom);
    JSValue extra;
    if (extraStatic(ctx, cls, atom, &extra)) return extra;
    return missingMember(ctx, cls, atom, m, Space::Static);
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
    // Antes virava propriedade JS no wrapper — que e novo a cada
    // `Terraria.Main`, entao `Main.dayTme = true` sumia sem dizer nada.
    missingMember(ctx, cls, atom, m, Space::Static, true);
    return -1;
}

/** `'Glyph' in SpriteFont`, `'PinkFairy' in DustID`: o mesmo que a leitura acha. */
int nc_exotic_has(JSContext* ctx, JSValueConst obj, JSAtom atom) {
    Il2CppClass* cls = classOf(obj);
    if (!cls) return false;
    const Member& m = member(ctx, cls, atom, Space::Static, g_nativeClassId);
    if (m.proto || m.found()) return true;
    return !m.quiet && extraStatic(ctx, cls, atom, nullptr);
}

JSValue no_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst receiver);

// QuickJS nao consulta get_property ao fazer Object.keys/for..in: precisa
// receber a lista de nomes e os descritores separadamente.
std::vector<std::string> nativeNames(Il2CppClass* cls, bool wantStatic) {
    auto& a = il2cpp::api();
    std::set<std::string> names;
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        void* it = nullptr;
        while (a.class_get_fields && a.field_get_name) {
            FieldInfo* f = a.class_get_fields(c, &it);
            if (!f) break;
            if (a.field_get_flags && ((a.field_get_flags(f) & 0x0010) != 0) != wantStatic) continue;
            if (const char* name = a.field_get_name(f)) names.emplace(name);
        }
        it = nullptr;
        while (const MethodInfo* m = a.class_get_methods(c, &it)) {
            if (a.method_get_flags && ((a.method_get_flags(m, nullptr) & 0x0010) != 0) != wantStatic) continue;
            const char* name = a.method_get_name(m);
            if (!name) continue;
            // A propriedade C# aparece pelo nome legivel; a assinatura exata
            // continua disponivel para metodos com overloads.
            if (std::strncmp(name, "get_", 4) == 0 && a.method_get_param_count(m) == 0)
                names.emplace(name + 4);
            else if (std::strncmp(name, "set_", 4) != 0 && name[0] != '.') {
                names.emplace(name);
                names.emplace(il2cpp::describeMethod(m));
            }
        }
    }
    return {names.begin(), names.end()};
}

int nativeOwnNames(JSContext* ctx, JSPropertyEnum** ptab, uint32_t* plen,
                   Il2CppClass* cls, bool wantStatic, JSClassID id) {
    *ptab = nullptr;
    *plen = 0;
    if (!cls) return 0;
    const auto names = nativeNames(cls, wantStatic);
    auto* out = static_cast<JSPropertyEnum*>(js_malloc(ctx, names.size() * sizeof(JSPropertyEnum)));
    if (!out && !names.empty()) return -1;
    for (const auto& name : names) {
        JSAtom atom = JS_NewAtom(ctx, name.c_str());
        if (atom == JS_ATOM_NULL) {
            for (uint32_t i = 0; i < *plen; ++i) JS_FreeAtom(ctx, out[i].atom);
            js_free(ctx, out);
            return -1;
        }
        const Member& m = member(ctx, cls, atom, wantStatic ? Space::Static : Space::Instance, id);
        if (!m.proto && (m.field || m.getter || m.method || m.nested)) {
            out[*plen].atom = atom;
            out[*plen].is_enumerable = true;
            ++*plen;
        } else JS_FreeAtom(ctx, atom);
    }
    *ptab = out;
    return 0;
}

int nativeOwnProperty(JSContext* ctx, JSPropertyDescriptor* desc, JSValueConst obj,
                      JSAtom atom, Il2CppClass* cls, bool wantStatic, JSClassID id) {
    if (!cls) return 0;
    const Member& m = member(ctx, cls, atom, wantStatic ? Space::Static : Space::Instance, id);
    if (m.proto || !(m.field || m.getter || m.method || m.nested)) return 0;
    if (desc) {
        desc->flags = JS_PROP_ENUMERABLE | JS_PROP_CONFIGURABLE | (m.field || m.setter ? JS_PROP_WRITABLE : 0);
        desc->getter = JS_UNDEFINED;
        desc->setter = JS_UNDEFINED;
        desc->value = wantStatic ? nc_exotic_get(ctx, obj, atom, obj) : no_exotic_get(ctx, obj, atom, obj);
        if (JS_IsException(desc->value)) return -1;
    }
    return 1;
}

int nc_own_names(JSContext* ctx, JSPropertyEnum** ptab, uint32_t* plen, JSValueConst obj) {
    return nativeOwnNames(ctx, ptab, plen, classOf(obj), true, g_nativeClassId);
}
int nc_own_property(JSContext* ctx, JSPropertyDescriptor* desc, JSValueConst obj, JSAtom atom) {
    return nativeOwnProperty(ctx, desc, obj, atom, classOf(obj), true, g_nativeClassId);
}

const JSClassExoticMethods nc_exotic = {
    nc_own_property, nc_own_names, nullptr, nullptr,
    nc_exotic_has, nc_exotic_get, nc_exotic_set,
};

// Tudo que estava aqui — getStaticInt, setStaticFloat, method(nome, aridade) —
// saiu: o acesso por propriedade e por assinatura faz o mesmo sem o modder
// precisar dizer o tipo nem contar parâmetros. O que fica no protótipo SOMBREIA
// nomes reais da classe do jogo, então quanto menos, melhor.
/** JSON.stringify(Terraria.Main): o nome, e nao todos os estaticos. */
JSValue nc_toJSON(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppClass* cls = classOf(self);
    return JS_NewString(ctx, ("[" + (cls ? className(cls) : std::string("?")) + "]").c_str());
}

const JSCFunctionListEntry nc_proto[] = {
    JS_CFUNC_DEF("toJSON", 1, nc_toJSON),
    JS_CFUNC_DEF("new", 0, nc_new),
    JS_CFUNC_DEF("newArray", 1, nc_newArray),
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
/**
 * O `this` de uma propriedade C# num objeto do jogo. Struct encaixotado (o que
 * `NetPacket.new()`, `Color.new()` devolvem): o metodo do struct quer os
 * DADOS, logo depois do cabecalho — como o gm_call ja faz nos metodos. Com a
 * caixa, `packet.Length` lia os bytes altos do ponteiro da classe.
 */
void* propertyThis(Il2CppObject* o, Il2CppClass* cls) {
    auto& a = il2cpp::api();
    if (a.class_is_valuetype && a.class_is_valuetype(cls)) return reinterpret_cast<char*>(o) + sizeof(Il2CppObject);
    return o;
}

JSValue no_exotic_get(JSContext* ctx, JSValueConst obj, JSAtom atom, JSValueConst) {
    Il2CppObject* o = objOf(obj);
    if (!o) return JS_UNDEFINED;
    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    const Member& m = member(ctx, cls, atom, Space::Instance, g_nativeObjectId);

    if (m.proto) return protoGet(ctx, g_nativeObjectId, atom);
    if (m.field) return readAt(ctx, reinterpret_cast<char*>(o) + m.offset, *m.type, obj);
    if (m.method) return makeGameMethod(ctx, m.method, obj);
    if (m.getter) return invokeGetter(ctx, m.getter, propertyThis(o, cls));
    if (m.signature) return missingSignature(ctx, cls, atom);
    // Campo que um mod pos na classe (bl.defineField): `item.ModItem`.
    if (isExtraField(cls, atom)) return extraFieldGet(ctx, o, atom);
    // Metodo que um mod pos na classe (bl.defineMethod): `player.GetModPlayer(X)`.
    JSValue method;
    if (extraMethodGet(ctx, cls, obj, atom, &method)) return method;
    return missingMember(ctx, cls, atom, m, Space::Instance);
}

int no_exotic_set(JSContext* ctx, JSValueConst obj, JSAtom atom,
                  JSValueConst value, JSValueConst, int) {
    Il2CppObject* o = objOf(obj);
    if (!o) return -1;
    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    const Member& m = member(ctx, cls, atom, Space::Instance, g_nativeObjectId);
    if (m.field) return writeAt(ctx, reinterpret_cast<char*>(o) + m.offset, *m.type, value);
    if (m.setter) return invokeSetter(ctx, m.setter, propertyThis(o, cls), value);
    if (isExtraField(cls, atom)) return extraFieldSet(ctx, o, atom, value);
    // Nome que a classe nao tem: RECUSA, como ja fazia o caminho do struct.
    // Antes isto virava uma propriedade JS comum no wrapper, entao um
    // `item.useTmie = 4` dava certo, nao mudava nada no jogo e nao dizia nada
    // — o tipo de erro que se procura por uma tarde inteira.
    missingMember(ctx, cls, atom, m, Space::Instance, true);
    return -1;
}

/**
 * `'ModItem' in item`, `'Item' in source`: sem isto o `in` so via o
 * prototipo JS e dizia false ate para `statLife`. E o jeito de perguntar por
 * um membro que pode nao existir, agora que ler um que falta e erro.
 */
int no_exotic_has(JSContext* ctx, JSValueConst obj, JSAtom atom) {
    Il2CppObject* o = objOf(obj);
    if (!o) return false;
    Il2CppClass* cls = il2cpp::api().object_get_class(o);
    const Member& m = member(ctx, cls, atom, Space::Instance, g_nativeObjectId);
    if (m.proto || m.found()) return true;
    return !m.quiet && (isExtraField(cls, atom) || hasExtraMethod(cls, atom));
}

void no_finalizer(JSRuntime*, JSValue val) {
    auto* p = static_cast<Pinned*>(JS_GetOpaque(val, g_nativeObjectId));
    forgetWrapper(g_objectWrappers, val, p);
    unpin(p);
}

const JSClassExoticMethods no_exotic = {
    [](JSContext* ctx, JSPropertyDescriptor* desc, JSValueConst obj, JSAtom atom) -> int {
        Il2CppObject* o = objOf(obj);
        return nativeOwnProperty(ctx, desc, obj, atom, o ? il2cpp::api().object_get_class(o) : nullptr,
                                 false, g_nativeObjectId);
    },
    [](JSContext* ctx, JSPropertyEnum** ptab, uint32_t* plen, JSValueConst obj) -> int {
        Il2CppObject* o = objOf(obj);
        return nativeOwnNames(ctx, ptab, plen, o ? il2cpp::api().object_get_class(o) : nullptr,
                              false, g_nativeObjectId);
    }, nullptr, nullptr,
    no_exotic_has, no_exotic_get, no_exotic_set,
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
    // O JSON.stringify para aqui. Com as chaves do objeto do jogo a mostra
    // (Object.keys, for..in), ele desceria no objeto inteiro: todo getter,
    // todo array e os objetos de dentro (Player -> inventory -> cada Item...).
    // E o bl.log({ player }) e o console passam pelo JSON sem ninguem pedir.
    JS_CFUNC_DEF("toJSON", 1, no_toString),
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
 * O array do jogo com os valores de `list` (array JS ou TypedArray), do tipo
 * `elem`. Cada valor e escrito como numa escrita `arr[i] = v`; um TypedArray
 * do mesmo tipo numerico (Uint8Array -> byte[], Float32Array -> float[]...)
 * vai numa copia so, sem passar valor por valor.
 */
Il2CppArray* arrayOfValues(JSContext* ctx, Il2CppClass* elem, JSValueConst list, const char* api) {
    auto& a = il2cpp::api();
    int64_t n = -1;
    JSValue len = JS_GetPropertyStr(ctx, list, "length");
    const int bad = JS_ToInt64(ctx, &n, len);
    JS_FreeValue(ctx, len);
    if (bad < 0) return nullptr;
    if (n < 0 || n > 0x7fffffff) {
        JS_ThrowRangeError(ctx, "%s: tamanho %lld invalido", api, static_cast<long long>(n));
        return nullptr;
    }
    Il2CppArray* arr = a.array_new(elem, static_cast<uintptr_t>(n));
    if (!arr) {
        JS_ThrowInternalError(ctx, "%s: o jogo recusou um %s[%lld]", api, className(elem).c_str(),
                              static_cast<long long>(n));
        return nullptr;
    }
    const TypeDesc& ed = describe(a.class_get_type(elem));
    const size_t stride = ed.byValue ? ed.size : sizeof(void*);
    auto* data = static_cast<char*>(arrayData(arr));

    const int typed = JS_GetTypedArrayType(list);
    if (typed >= 0 && !ed.isEnum && ed.byValue && n > 0) {
        Prim same = Prim::Void;
        switch (typed) {
            case JS_TYPED_ARRAY_UINT8C:
            case JS_TYPED_ARRAY_UINT8:      same = Prim::U8; break;
            case JS_TYPED_ARRAY_INT8:       same = Prim::I8; break;
            case JS_TYPED_ARRAY_INT16:      same = Prim::I16; break;
            case JS_TYPED_ARRAY_UINT16:     same = ed.prim == Prim::Char ? Prim::Char : Prim::U16; break;
            case JS_TYPED_ARRAY_INT32:      same = Prim::I32; break;
            case JS_TYPED_ARRAY_UINT32:     same = Prim::U32; break;
            case JS_TYPED_ARRAY_BIG_INT64:  same = Prim::I64; break;
            case JS_TYPED_ARRAY_BIG_UINT64: same = Prim::U64; break;
            case JS_TYPED_ARRAY_FLOAT32:    same = Prim::F32; break;
            case JS_TYPED_ARRAY_FLOAT64:    same = Prim::F64; break;
            default: break;
        }
        if (same == ed.prim) {
            size_t offset = 0, bytes = 0, per = 0;
            JSValue buffer = JS_GetTypedArrayBuffer(ctx, list, &offset, &bytes, &per);
            if (JS_IsException(buffer)) return nullptr;
            size_t size = 0;
            uint8_t* raw = JS_GetArrayBuffer(ctx, &size, buffer);
            JS_FreeValue(ctx, buffer);
            if (!raw || per != ed.size || offset + bytes > size) {
                JS_ThrowTypeError(ctx, "%s: nao consegui ler o TypedArray", api);
                return nullptr;
            }
            std::memcpy(data, raw + offset, static_cast<size_t>(n) * ed.size);
            return arr;
        }
    }

    for (int64_t i = 0; i < n; ++i) {
        JSValue x = JS_GetPropertyInt64(ctx, list, i);
        if (JS_IsException(x)) return nullptr;
        const int r = writeAt(ctx, data + static_cast<size_t>(i) * stride, ed, x);
        JS_FreeValue(ctx, x);
        if (r < 0) {
            // Diz qual posicao, para uma lista longa.
            JSValue e = JS_GetException(ctx);
            const std::string why = logText(ctx, e);
            JS_FreeValue(ctx, e);
            JS_ThrowTypeError(ctx, "%s: posicao %lld: %s", api, static_cast<long long>(i), why.c_str());
            return nullptr;
        }
    }
    return arr;
}

/**
 * O tipo do elemento: pelo nome do C# ('int', 'byte', 'bool', 'short',
 * 'ushort', 'uint', 'long', 'ulong', 'sbyte', 'float', 'double', 'char',
 * 'string', 'object'), por um nome do System ('Int32') ou completo
 * ('Terraria.Item'), pela classe (Terraria.Item) ou por um ajudante que tem a
 * classe em `.Type` (Vector2, Color, Rectangle).
 */
Il2CppClass* elementClassOf(JSContext* ctx, JSValueConst t, const char* api) {
    if (JS_IsString(t)) {
        static const std::unordered_map<std::string, const char*> alias = {
            {"bool", "Boolean"}, {"byte", "Byte"}, {"sbyte", "SByte"}, {"short", "Int16"},
            {"ushort", "UInt16"}, {"int", "Int32"}, {"uint", "UInt32"}, {"long", "Int64"},
            {"ulong", "UInt64"}, {"float", "Single"}, {"double", "Double"}, {"char", "Char"},
            {"string", "String"}, {"object", "Object"},
        };
        const char* cs = JS_ToCString(ctx, t);
        if (!cs) return nullptr;
        const std::string name = cs;
        JS_FreeCString(ctx, cs);
        std::string ns = "System", simple = name;
        if (auto it = alias.find(name); it != alias.end()) {
            simple = it->second;
        } else if (auto dot = name.rfind('.'); dot != std::string::npos) {
            ns = name.substr(0, dot);
            simple = name.substr(dot + 1);
        }
        Il2CppClass* c = il2cpp::findClassQuiet(ns.c_str(), simple.c_str());
        if (!c) JS_ThrowTypeError(ctx, "%s: tipo '%s' nao encontrado", api, name.c_str());
        return c;
    }
    if (Il2CppClass* c = classOf(t)) return c;
    if (JS_IsObject(t)) {
        JSValue inner = JS_GetPropertyStr(ctx, t, "Type");
        Il2CppClass* c = nullptr;
        if (JS_IsException(inner)) JS_FreeValue(ctx, JS_GetException(ctx));
        else c = classOf(inner);
        JS_FreeValue(ctx, inner);
        if (c) return c;
    }
    JS_ThrowTypeError(ctx, "%s: passe o tipo do elemento ('int', 'byte'... ou uma classe do jogo)", api);
    return nullptr;
}

/**
 * Classe.newArray(n): o `new T[n]` do C#, com as posicoes zeradas (0, null,
 * struct zerado). Classe.newArray([a, b, c]): do tamanho da lista, ja
 * preenchido. Sai como os outros arrays do jogo: [i], length e cloneResized.
 *
 *   Terraria.Item.newArray(10)            // Item[10]
 *   System.Int32.newArray([1, 2, 3])      // int[] { 1, 2, 3 }
 */
JSValue nc_newArray(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppClass* cls = classOf(self);
    if (!cls) return JS_EXCEPTION;
    auto& a = il2cpp::api();
    if (!a.array_new) return JS_ThrowInternalError(ctx, "newArray: este runtime nao expoe il2cpp_array_new");
    if (argc < 1) return JS_ThrowTypeError(ctx, "%s.newArray(tamanho) ou newArray([valores])", className(cls).c_str());
    // Como o `new` do C#: o construtor estatico do tipo roda antes.
    if (a.runtime_class_init) a.runtime_class_init(cls);

    if (JS_IsNumber(argv[0])) {
        double d = 0;
        if (JS_ToFloat64(ctx, &d, argv[0]) < 0) return JS_EXCEPTION;
        if (d != static_cast<double>(static_cast<int64_t>(d)) || d < 0 || d > 0x7fffffff) {
            return JS_ThrowRangeError(ctx, "%s.newArray: tamanho %g invalido", className(cls).c_str(), d);
        }
        Il2CppArray* arr = a.array_new(cls, static_cast<uintptr_t>(d));
        if (!arr) return JS_ThrowInternalError(ctx, "%s.newArray: o jogo recusou o array", className(cls).c_str());
        return makeGameArray(ctx, arr);
    }
    if (!JS_IsArray(argv[0]) && JS_GetTypedArrayType(argv[0]) < 0) {
        return JS_ThrowTypeError(ctx, "%s.newArray: passe o tamanho ou uma lista de valores", className(cls).c_str());
    }
    Il2CppArray* arr = arrayOfValues(ctx, cls, argv[0], "newArray");
    return arr ? makeGameArray(ctx, arr) : JS_EXCEPTION;
}

/**
 * [1, 2, 3].makeGeneric('int'): o array JS vira um array do jogo, como no TL
 * Pro. Tambem num TypedArray: `bytes.makeGeneric('byte')`.
 *   ['a', 'b'].makeGeneric('string')         // string[]
 *   [v1, v2].makeGeneric(Vector2)            // Vector2[]
 *   [].makeGeneric('byte').cloneResized(n)   // byte[n] zerado
 */
JSValue js_arrayMakeGeneric(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    auto& a = il2cpp::api();
    if (!a.array_new) return JS_ThrowInternalError(ctx, "makeGeneric: este runtime nao expoe il2cpp_array_new");
    if (argc < 1) {
        return JS_ThrowTypeError(ctx, "lista.makeGeneric(tipo): passe o tipo do elemento ('int', 'byte'... ou uma classe)");
    }
    Il2CppClass* elem = elementClassOf(ctx, argv[0], "makeGeneric");
    if (!elem) return JS_EXCEPTION;
    if (a.runtime_class_init) a.runtime_class_init(elem);
    Il2CppArray* arr = arrayOfValues(ctx, elem, self, "makeGeneric");
    return arr ? makeGameArray(ctx, arr) : JS_EXCEPTION;
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

void nm_finalizer(JSRuntime* rt, JSValue val) {
    if (auto* r = static_cast<MethodRef*>(JS_GetOpaque(val, g_nativeMethodId))) {
        JS_FreeValueRT(rt, r->owner);
        std::free(r);
    }
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
        thisPtr = self(JS_IsUndefined(r->owner) ? thisVal : r->owner);
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

// NativeMethod.hook(callback[, { minType, on, field, tile, tileAt, arg, marks, whileIn, ifBusy }])
//
// Com `minType`, o hook so chama o JS quando o objeto `on` ('self', o padrao,
// ou o indice de um parametro) tem `field` (padrao 'type') >= minType. Ex.:
// NPC.AI so para NPC de mod — os do jogo nem entram no JS.
// Com `whileIn` (outro metodo, ja hookado), so chama o JS enquanto a thread
// esta dentro do hook dele.
// Com `ifBusy` ('original' ou 'skip'), nao espera o motor JS que esta noutra
// thread: roda so o metodo do jogo, ou nada (metodo void). Ver IfBusy.
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
        JSValue marks = JS_GetPropertyStr(ctx, argv[1], "marks");
        if (JS_IsString(marks)) {
            const char* s = JS_ToCString(ctx, marks);
            if (s) { filter.marks = s; JS_FreeCString(ctx, s); }
        }
        JS_FreeValue(ctx, marks);
        JSValue busy = JS_GetPropertyStr(ctx, argv[1], "ifBusy");
        if (!JS_IsUndefined(busy)) {
            const char* s = JS_ToCString(ctx, busy);
            const std::string mode = s ? s : "";
            if (s) JS_FreeCString(ctx, s);
            JS_FreeValue(ctx, busy);
            if (mode == "original") filter.ifBusy = IfBusy::Original;
            else if (mode == "skip") filter.ifBusy = IfBusy::Skip;
            else if (mode != "wait") return JS_ThrowTypeError(ctx, "hook: ifBusy e 'wait', 'original' ou 'skip'");
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
        JSValue tile = JS_GetPropertyStr(ctx, argv[1], "tile");
        JSValue tileAt = JS_GetPropertyStr(ctx, argv[1], "tileAt");
        JSValue arg = JS_GetPropertyStr(ctx, argv[1], "arg");
        int32_t n = 0;
        const bool ok = JS_ToInt32(ctx, &n, min) == 0;
        filter.minType = n;
        // `tile: indice` ou `tileAt: [i, j]`: o tipo vem do mundo, nao de um
        // campo de objeto.
        const bool byTile = JS_IsNumber(tile) || JS_IsArray(tileAt) || JS_IsNumber(arg);
        if (JS_IsNumber(arg)) JS_ToInt32(ctx, &filter.argParam, arg);
        JS_FreeValue(ctx, arg);
        if (JS_IsNumber(tile)) JS_ToInt32(ctx, &filter.tileParam, tile);
        if (JS_IsArray(tileAt)) {
            JSValue i = JS_GetPropertyUint32(ctx, tileAt, 0), j = JS_GetPropertyUint32(ctx, tileAt, 1);
            JS_ToInt32(ctx, &filter.tileAtI, i);
            JS_ToInt32(ctx, &filter.tileAtJ, j);
            JS_FreeValue(ctx, i);
            JS_FreeValue(ctx, j);
        }
        JS_FreeValue(ctx, tile);
        JS_FreeValue(ctx, tileAt);
        filter.on = byTile ? -2 : -1;
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
                       filter.on == -2 && !filter.whileIn && filter.tileParam < 0 && filter.tileAtI < 0 &&
                               filter.argParam < 0 && filter.marks.empty() &&
                               filter.ifBusy == IfBusy::Wait
                           ? nullptr : &filter))
        return JS_EXCEPTION;
    return JS_UNDEFINED;
}


// bl.hookStats(): [{ name, calls, js, jsMs }] por hook instalado, contados desde a
// instalacao. `calls` inclui as chamadas que o filtro nativo devolve direto ao
// jogo: e o custo de um hook num metodo quente (SpriteBatch.Draw).
JSValue js_hookStats(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    const std::vector<HookStat> stats = hookStats();
    JSValue arr = JS_NewArray(ctx);
    uint32_t i = 0;
    for (const HookStat& s : stats) {
        JSValue o = JS_NewObject(ctx);
        JS_SetPropertyStr(ctx, o, "name", JS_NewString(ctx, s.name.c_str()));
        JS_SetPropertyStr(ctx, o, "calls", JS_NewFloat64(ctx, static_cast<double>(s.calls)));
        JS_SetPropertyStr(ctx, o, "js", JS_NewFloat64(ctx, static_cast<double>(s.js)));
        JS_SetPropertyStr(ctx, o, "jsMs", JS_NewFloat64(ctx, static_cast<double>(s.jsNs) / 1e6));
        JS_SetPropertyUint32(ctx, arr, i++, o);
    }
    return arr;
}

// bl.gcThreshold(): o limiar de memoria do coletor de ciclos do QuickJS. Ele
// muda a cada coleta (vira 1,5 x a memoria de entao): contar as mudancas por
// quadro da quantas coletas houve.
JSValue js_gcThreshold(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    return JS_NewFloat64(ctx, static_cast<double>(JS_GetGCThreshold(JS_GetRuntime(ctx))));
}

// bl.gc(): roda uma coleta de ciclos agora e devolve quanto levou (ms). Para
// medir o custo de uma coleta com os objetos vivos dos mods.
JSValue js_gc(JSContext* ctx, JSValueConst, int, JSValueConst*) {
    timespec a, b;
    clock_gettime(CLOCK_MONOTONIC, &a);
    JS_RunGC(JS_GetRuntime(ctx));
    clock_gettime(CLOCK_MONOTONIC, &b);
    return JS_NewFloat64(ctx, (b.tv_sec - a.tv_sec) * 1e3 + (b.tv_nsec - a.tv_nsec) / 1e6);
}

// bl.hookMarks.set(nome, tipo, ligado = true) / .has(nome, tipo): a tabela que
// o filtro `marks` de um hook consulta. Marcar depois de instalar o hook vale
// na hora (o despachante le a cada chamada).
JSValue js_hookMarksSet(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 2 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.hookMarks.set(nome, tipo[, ligado])");
    int32_t type = -1;
    JS_ToInt32(ctx, &type, argv[1]);
    const bool on = argc < 3 || JS_ToBool(ctx, argv[2]) > 0;
    std::atomic<uint8_t>* marks = hookMarks(name);
    JS_FreeCString(ctx, name);
    if (type < 0 || type >= kMarkTypes) return JS_ThrowRangeError(ctx, "bl.hookMarks.set: tipo %d fora de 0..%d", type, kMarkTypes - 1);
    marks[type].store(on ? 1 : 0, std::memory_order_relaxed);
    return JS_UNDEFINED;
}

JSValue js_hookMarksHas(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* name = argc >= 2 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!name) return JS_ThrowTypeError(ctx, "bl.hookMarks.has(nome, tipo)");
    int32_t type = -1;
    JS_ToInt32(ctx, &type, argv[1]);
    std::atomic<uint8_t>* marks = hookMarks(name);
    JS_FreeCString(ctx, name);
    return JS_NewBool(ctx, type >= 0 && type < kMarkTypes && marks[type].load(std::memory_order_relaxed));
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

// indexOf (o indice de um atomo) mora em Members.cpp: o struct com indexador
// (proj.ai[0], em Value.cpp) usa o mesmo.

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

/** O nome e do prototipo do array (toString, e o que vem de Object)? */
bool gaProtoHas(JSContext* ctx, JSAtom atom) {
    JSValue proto = JS_GetClassProto(ctx, g_gameArrayId);
    const int has = JS_HasProperty(ctx, proto, atom);
    JS_FreeValue(ctx, proto);
    return has > 0;
}

/** Um array do jogo so tem [i], length, cloneResized, fill, empty e find — nao os outros do Array do JS. */
JSValue missingArrayMember(JSContext* ctx, Il2CppArray* arr, JSAtom atom) {
    const std::string owner =
        className(il2cpp::api().object_get_class(reinterpret_cast<Il2CppObject*>(arr)));
    return JS_ThrowTypeError(ctx, "Member with name '%s' is not found in %s "
                                  "(um array do jogo tem [i], length, cloneResized, fill, empty e find; "
                                  "para os outros, Array.from(arr))",
                             atomName(ctx, atom).c_str(), owner.c_str());
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
        if (gaProtoHas(ctx, atom)) return protoGet(ctx, g_gameArrayId, atom);
        if (engineProbe(ctx, atom)) return JS_UNDEFINED;
        return missingArrayMember(ctx, arr, atom);
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
        if (atom == lengthAtom(ctx)) {
            JS_ThrowTypeError(ctx, "%s: length e somente leitura; para outro tamanho, "
                                   "atribua arr.cloneResized(n) ao campo do jogo",
                              className(il2cpp::api().object_get_class(
                                  reinterpret_cast<Il2CppObject*>(arr))).c_str());
            return -1;
        }
        missingArrayMember(ctx, arr, atom);
        return -1;
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

/** `i in arr` e `'length' in arr`. */
int ga_exotic_has(JSContext* ctx, JSValueConst obj, JSAtom atom) {
    Il2CppArray* arr = arrayFromJS(obj);
    if (!arr) return false;
    const int64_t idx = indexOf(ctx, atom);
    if (idx >= 0) return static_cast<uint64_t>(idx) < arr->length;
    static JSAtom cloneAtom = JS_NewAtom(ctx, "cloneResized");
    return atom == lengthAtom(ctx) || atom == cloneAtom || gaProtoHas(ctx, atom);
}

/** "Item[400]", em vez do "?" que o bl.log mostrava. */
JSValue ga_toString(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppArray* arr = arrayFromJS(self);
    if (!arr) return JS_ThrowTypeError(ctx, "toString: chame num array do jogo");
    auto& a = il2cpp::api();
    const char* cn = a.class_get_name(a.object_get_class(reinterpret_cast<Il2CppObject*>(arr)));
    std::string name = cn ? cn : "?";
    if (name.size() > 2 && name.compare(name.size() - 2, 2, "[]") == 0) name.resize(name.size() - 2);
    name += "[" + std::to_string(static_cast<unsigned long long>(arr->length)) + "]";
    return JS_NewString(ctx, name.c_str());
}

/** Um indice relativo como o do Array do JS: negativo conta do fim; preso em 0..len. */
int relativeIndex(JSContext* ctx, JSValueConst v, int64_t len, int64_t dflt, int64_t* out) {
    if (JS_IsUndefined(v)) {
        *out = dflt;
        return 0;
    }
    double d = 0;
    if (JS_ToFloat64(ctx, &d, v) < 0) return -1;
    if (d != d) d = 0;   // NaN
    const double t = d < 0 ? -std::floor(-d) : std::floor(d);
    const double r = t < 0 ? std::max(static_cast<double>(len) + t, 0.0) : std::min(t, static_cast<double>(len));
    *out = static_cast<int64_t>(r);
    return 0;
}

/**
 * arr.fill(valor, inicio?, fim?): como o fill do Array do JS, no proprio array
 * do jogo (muda o jogo) e devolve ele. O valor e convertido uma vez, como numa
 * escrita `arr[i] = v`, e copiado para as outras posicoes.
 */
JSValue ga_fill(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppArray* arr = arrayFromJS(self);
    if (!arr) return JS_ThrowTypeError(ctx, "fill: chame num array do jogo");
    const int64_t len = static_cast<int64_t>(arr->length);
    int64_t start = 0, end = len;
    if (relativeIndex(ctx, argc > 1 ? argv[1] : JS_UNDEFINED, len, 0, &start) < 0) return JS_EXCEPTION;
    if (relativeIndex(ctx, argc > 2 ? argv[2] : JS_UNDEFINED, len, len, &end) < 0) return JS_EXCEPTION;
    if (start >= end) return JS_DupValue(ctx, self);

    const TypeDesc& d = elemOf(ctx, self, arr);
    if (!d.size) return JS_EXCEPTION;
    const size_t stride = strideOf(d);
    auto* base = static_cast<char*>(arrayData(arr));
    char* first = base + static_cast<size_t>(start) * stride;
    if (writeAt(ctx, first, d, argc > 0 ? argv[0] : JS_UNDEFINED) < 0) return JS_EXCEPTION;

    auto& a = il2cpp::api();
    for (int64_t i = start + 1; i < end; ++i) {
        char* slot = base + static_cast<size_t>(i) * stride;
        if (d.byValue) {
            std::memcpy(slot, first, stride);
        } else if (a.gc_wbarrier_set_field) {
            a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(arr), reinterpret_cast<void**>(slot),
                                    *reinterpret_cast<Il2CppObject**>(first));
        } else {
            *reinterpret_cast<void**>(slot) = *reinterpret_cast<void**>(first);
        }
    }
    return JS_DupValue(ctx, self);
}

/** arr.empty(): um array NOVO do mesmo tipo, sem posicoes (o cloneResized(0)). */
JSValue ga_empty(JSContext* ctx, JSValueConst self, int, JSValueConst*) {
    Il2CppArray* arr = arrayFromJS(self);
    if (!arr) return JS_ThrowTypeError(ctx, "empty: chame num array do jogo");
    Il2CppArray* copy = runtime::TypeTables::resizedCopy(arr, 0);
    if (!copy) return JS_ThrowInternalError(ctx, "empty: o jogo recusou o array");
    return makeGameArray(ctx, copy);
}

/**
 * arr.find(fn, thisArg?): como o find do Array do JS: o primeiro elemento com
 * fn(elemento, indice, arr) verdadeiro, ou undefined. O elemento e o mesmo de
 * `arr[i]` (um struct sai como vista para dentro do array).
 */
JSValue ga_find(JSContext* ctx, JSValueConst self, int argc, JSValueConst* argv) {
    Il2CppArray* arr = arrayFromJS(self);
    if (!arr) return JS_ThrowTypeError(ctx, "find: chame num array do jogo");
    if (argc < 1 || !JS_IsFunction(ctx, argv[0])) {
        return JS_ThrowTypeError(ctx, "find(fn): passe uma funcao (elemento, indice, array) => bool");
    }
    JSValueConst thisArg = argc > 1 ? argv[1] : JS_UNDEFINED;
    const int64_t len = static_cast<int64_t>(arr->length);
    for (int64_t i = 0; i < len; ++i) {
        JSValue elem = JS_GetPropertyInt64(ctx, self, i);
        if (JS_IsException(elem)) return elem;
        JSValue args[3] = {elem, JS_NewInt64(ctx, i), JS_DupValue(ctx, self)};
        JSValue r = JS_Call(ctx, argv[0], thisArg, 3, args);
        JS_FreeValue(ctx, args[1]);
        JS_FreeValue(ctx, args[2]);
        if (JS_IsException(r)) {
            JS_FreeValue(ctx, elem);
            return r;
        }
        const int truthy = JS_ToBool(ctx, r);
        JS_FreeValue(ctx, r);
        if (truthy > 0) return elem;
        JS_FreeValue(ctx, elem);
    }
    return JS_UNDEFINED;
}

const JSCFunctionListEntry ga_proto[] = {
    JS_CFUNC_DEF("toString", 0, ga_toString),
    JS_CFUNC_DEF("fill", 1, ga_fill),
    JS_CFUNC_DEF("empty", 0, ga_empty),
    JS_CFUNC_DEF("find", 1, ga_find),
};

const JSClassExoticMethods ga_exotic = {
    nullptr, nullptr, nullptr, nullptr,
    ga_exotic_has, ga_exotic_get, ga_exotic_set,
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

namespace {

/** Um GameMethod novo; `owner` (ou undefined) e o `this` das chamadas. */
JSValue newGameMethod(JSContext* ctx, const MethodInfo* m, JSValueConst owner) {
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
    ref->owner = JS_DupValue(ctx, owner);
    auto& a = il2cpp::api();
    Il2CppClass* declaring = a.method_get_class(m);
    ref->thisIsObject = ref->isInstance && declaring && a.class_is_valuetype &&
                        !a.class_is_valuetype(declaring);
    JSValue v = JS_NewObjectClass(ctx, g_nativeMethodId);
    if (JS_IsException(v)) {
        JS_FreeValue(ctx, ref->owner);
        std::free(ref);
        return v;
    }
    JS_SetOpaque(v, ref);
    return v;
}

/**
 * Os metodos presos ao objeto de onde foram lidos (`const f = npc.Foo; f()`
 * chama no npc), numa tabela pequena de acesso direto por (objeto, metodo).
 * Sem ela, cada `npc['void Foo()']()` de um laco por quadro montava um
 * GameMethod novo — o custo que o cache de baixo tirou. A vaga guarda o
 * metodo, que segura o objeto: a chave (o endereco do embrulho) nao fica
 * velha enquanto esta ali. Colisao troca a vaga; o antigo e solto.
 */
struct BoundSlot {
    bool used = false;
    const void* owner = nullptr;
    const MethodInfo* method = nullptr;
    JSValue value;
};
// Os lacos de mod passam por Main.projectile (1000), Main.item (400),
// Main.player (255) e Main.npc (200): com 512 vagas um laco de projeteis
// ja colidia consigo mesmo. 4096 (~128 KB) deixa esses lacos com poucas
// colisoes; os objetos que a tabela segura vivos sao os fixos do Main.
constexpr size_t kBoundSlots = 4096;
BoundSlot g_bound[kBoundSlots];

} // namespace

JSValue makeGameMethod(JSContext* ctx, const MethodInfo* m, JSValueConst owner) {
    // Um GameMethod por metodo, criado uma vez: `obj['void Foo()']()` num laco
    // montava um objeto novo (malloc + objeto JS + method_get_flags) a cada
    // volta — 290 ns jogados fora por chamada (docs/historico/PONTE-OTIMIZACAO.md,
    // passo 3). O cache segura a referencia para sempre, como os callbacks de
    // hook: o runtime JS vive ate o processo morrer (shutdown() nao e chamado).
    if (JS_IsUndefined(owner)) {
        static std::unordered_map<const MethodInfo*, JSValue> cache;
        auto hit = cache.find(m);
        if (hit != cache.end()) return JS_DupValue(ctx, hit->second);
        JSValue v = newGameMethod(ctx, m, owner);
        if (!JS_IsException(v)) cache.emplace(m, JS_DupValue(ctx, v));
        return v;
    }

    const void* key = JS_VALUE_GET_PTR(owner);
    const uintptr_t h = (reinterpret_cast<uintptr_t>(key) >> 4) ^
                        (reinterpret_cast<uintptr_t>(m) >> 3) * 0x9E3779B1u;
    BoundSlot& slot = g_bound[h % kBoundSlots];
    if (slot.used && slot.owner == key && slot.method == m) return JS_DupValue(ctx, slot.value);
    JSValue v = newGameMethod(ctx, m, owner);
    if (JS_IsException(v)) return v;
    if (slot.used) JS_FreeValue(ctx, slot.value);
    slot.used = true;
    slot.owner = key;
    slot.method = m;
    slot.value = JS_DupValue(ctx, v);
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

/**
 * Uma classe que o jogo nao tem, mas o tModLoader tem (`Terraria.ID.ItemRarityID`):
 * a tabela `__blExtraClasses` do ModHelpers.js, por namespace. So depois de o
 * jogo nao achar, entao a classe do jogo sempre ganha.
 */
bool extraClass(JSContext* ctx, const std::string& ns, JSAtom atom, JSValue* out) {
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue extras = JS_GetPropertyStr(ctx, global, "__blExtraClasses");
    JS_FreeValue(ctx, global);
    bool found = false;
    if (JS_IsObject(extras)) {
        JSValue table = JS_GetPropertyStr(ctx, extras, ns.c_str());
        if (JS_IsObject(table) && JS_HasProperty(ctx, table, atom) > 0) {
            found = true;
            *out = JS_GetProperty(ctx, table, atom);
        }
        JS_FreeValue(ctx, table);
    }
    JS_FreeValue(ctx, extras);
    return found;
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
    JSValue extra;
    if (extraClass(ctx, *prefix, atom, &extra)) return extra;
    return makeNamespace(ctx, prefix->empty() ? name : *prefix + "." + name);
}

const JSClassExoticMethods ns_exotic = {
    nullptr, nullptr, nullptr, nullptr, nullptr, ns_exotic_get, nullptr,
};

/** bl.classOf(ns, nome) — quando a árvore não ajuda. */
JSValue js_loadTexture(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return loadTexture(ctx, argc, argv);
}

JSValue js_loadTextureAsset(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    return loadTextureAsset(ctx, argc, argv);
}

JSValue js_classOf(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "bl.classOf(namespace, nome)");
    return js_NativeClass(ctx, JS_UNDEFINED, argc, argv);
}

/**
 * bl.box(valor, tipo): o valor JS encaixotado como `object` do C#, para onde o
 * jogo quer `object` (Array.SetValue, Hashtable, parametro generico fechado em
 * object). O tipo e o mesmo do makeGeneric: 'int', 'float', 'string'..., um
 * nome completo, ou a classe (enum e struct tambem).
 *
 *   SetValue(arr, bl.box(20, 'int'), 0)
 *
 * Tipo de referencia nao tem caixa: devolve o proprio objeto, conferido.
 */
JSValue js_box(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 2) return JS_ThrowTypeError(ctx, "bl.box(valor, tipo)");
    Il2CppClass* cls = elementClassOf(ctx, argv[1], "bl.box");
    if (!cls) return JS_EXCEPTION;
    auto& a = il2cpp::api();
    const TypeDesc& d = describe(a.class_get_type(cls));
    if (!d.byValue) {
        // Texto vira System.String; objeto passa pela conferencia de tipo.
        void* slot = nullptr;
        if (writeAt(ctx, &slot, d, argv[0]) < 0) return JS_EXCEPTION;
        return slot ? makeNativeObject(ctx, static_cast<Il2CppObject*>(slot)) : JS_NULL;
    }
    if (!a.value_box) return JS_ThrowInternalError(ctx, "bl.box: este runtime nao expoe il2cpp_value_box");
    std::vector<char> buf(d.size ? d.size : 1, 0);
    if (writeAt(ctx, buf.data(), d, argv[0]) < 0) return JS_EXCEPTION;
    Il2CppObject* boxed = a.value_box(cls, buf.data());
    return boxed ? makeNativeObject(ctx, boxed) : JS_NULL;
}

/**
 * bl.unbox(obj): o inverso. Numero, bool, char (como numero), enum (o valor)
 * e texto voltam como valor JS; struct volta como COPIA (a caixa e do coletor
 * do jogo). Objeto que nao e caixa, array e valor JS voltam como estao.
 *
 *   bl.unbox(GetValue(arr, 0))   // 20
 */
JSValue js_unbox(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_ThrowTypeError(ctx, "bl.unbox(objeto)");
    Il2CppObject* o = objectFromJS(argv[0]);
    if (!o || isArrayObject(o)) return JS_DupValue(ctx, argv[0]);
    auto& a = il2cpp::api();
    Il2CppClass* cls = a.object_get_class(o);
    const TypeDesc& d = describe(cls ? a.class_get_type(cls) : nullptr);
    if (d.prim == Prim::String) return readAt(ctx, &o, d, JS_UNDEFINED);
    if (!d.byValue) return JS_DupValue(ctx, argv[0]);
    void* data = reinterpret_cast<char*>(o) + sizeof(Il2CppObject);
    if (d.prim == Prim::Struct) return makeStructCopy(ctx, cls, data, d.size);
    return readAt(ctx, data, d, JS_UNDEFINED);
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
    BL_DEBUG("namespaces: %d classe(s) sem namespace publicadas como globais", published);
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
    BL_DEBUG("namespaces: %zu raizes publicadas", roots.size());
    installGlobalClasses(ctx, global);
}

std::string valueToLogText(JSContext* ctx, JSValueConst v) { return logText(ctx, v); }

void installBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSRuntime* rt = JS_GetRuntime(ctx);
    JSValue global = JS_GetGlobalObject(ctx);

    // `bl` de Bunny Loader. Era `tl`, herdado de espelhar a API do TL Pro —
    // nome de outro produto na API pública do nosso não faz sentido.
    JSValue bl = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, bl, "log", JS_NewCFunction(ctx, js_bl_log, "log", 1));
    JS_SetPropertyStr(ctx, bl, "classOf", JS_NewCFunction(ctx, js_classOf, "classOf", 2));
    JS_SetPropertyStr(ctx, bl, "box", JS_NewCFunction(ctx, js_box, "box", 2));
    JS_SetPropertyStr(ctx, bl, "unbox", JS_NewCFunction(ctx, js_unbox, "unbox", 1));
    JS_SetPropertyStr(ctx, bl, "loadTexture",
                      JS_NewCFunction(ctx, js_loadTexture, "loadTexture", 1));
    JS_SetPropertyStr(ctx, bl, "loadTextureAsset",
                      JS_NewCFunction(ctx, js_loadTextureAsset, "loadTextureAsset", 1));
    JSValue marks = JS_NewObject(ctx);
    JS_SetPropertyStr(ctx, marks, "set", JS_NewCFunction(ctx, js_hookMarksSet, "set", 3));
    JS_SetPropertyStr(ctx, marks, "has", JS_NewCFunction(ctx, js_hookMarksHas, "has", 2));
    JS_SetPropertyStr(ctx, bl, "hookMarks", marks);
    JS_SetPropertyStr(ctx, bl, "hookStats", JS_NewCFunction(ctx, js_hookStats, "hookStats", 0));
    JS_SetPropertyStr(ctx, bl, "gcThreshold", JS_NewCFunction(ctx, js_gcThreshold, "gcThreshold", 0));
    JS_SetPropertyStr(ctx, bl, "gc", JS_NewCFunction(ctx, js_gc, "gc", 0));
    installExtraFields(ctx, bl);
    installItemsApi(ctx, bl);
    installProjectilesApi(ctx, bl);
    installBuffsApi(ctx, bl);
    installTilesApi(ctx, bl);
    installFilesApi(ctx, bl);
    installNpcsApi(ctx, bl);
    installSoundsApi(ctx, bl);
    installConsoleApi(ctx, global, bl);
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
    JSValue gaProto = JS_NewObject(ctx);
    JS_SetPropertyFunctionList(ctx, gaProto, ga_proto, sizeof(ga_proto)/sizeof(ga_proto[0]));
    JS_SetClassProto(ctx, g_gameArrayId, gaProto);

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

    // [1, 2].makeGeneric('int') (como no TL Pro): no Array e no prototipo comum
    // dos TypedArray, fora do for-in.
    {
        JSValue fn = JS_NewCFunction(ctx, js_arrayMakeGeneric, "makeGeneric", 1);
        JSValue arrayCtor = JS_GetPropertyStr(ctx, global, "Array");
        JSValue arrayProto = JS_GetPropertyStr(ctx, arrayCtor, "prototype");
        JS_DefinePropertyValueStr(ctx, arrayProto, "makeGeneric", JS_DupValue(ctx, fn),
                                  JS_PROP_WRITABLE | JS_PROP_CONFIGURABLE);
        JSValue u8 = JS_GetPropertyStr(ctx, global, "Uint8Array");
        JSValue u8Proto = JS_GetPropertyStr(ctx, u8, "prototype");
        JSValue typedProto = JS_GetPrototype(ctx, u8Proto);
        if (JS_IsObject(typedProto)) {
            JS_DefinePropertyValueStr(ctx, typedProto, "makeGeneric", JS_DupValue(ctx, fn),
                                      JS_PROP_WRITABLE | JS_PROP_CONFIGURABLE);
        }
        JS_FreeValue(ctx, typedProto);
        JS_FreeValue(ctx, u8Proto);
        JS_FreeValue(ctx, u8);
        JS_FreeValue(ctx, arrayProto);
        JS_FreeValue(ctx, arrayCtor);
        JS_FreeValue(ctx, fn);
    }

    JS_FreeValue(ctx, global);
    BL_DEBUG("bindings instalados (bl.log/classOf, arvore de namespaces)");
}

#else

void installBindings(void*) {}

#endif

} // namespace bl::script
