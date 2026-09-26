#include "script/bridge/Invoke.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/bridge/Marshal.h"
#include "script/bridge/Ref.h"
#include "script/bridge/Value.h"

#if defined(__aarch64__)
#include "script/bridge/Abi.h"
#endif

#include <cstring>
#include <unordered_map>
#include <vector>

namespace bl::script {

namespace {

/** Caminho lento, e o que sempre funcionou: empacota e deixa o runtime chamar. */
JSValue viaRuntimeInvoke(JSContext* ctx, const MethodInfo* m, void* self,
                         int argc, JSValueConst* argv) {
    ArgPack pack;
    if (!pack.build(ctx, m, argc, argv)) return JS_EXCEPTION;
    Il2CppObject* exc = nullptr;
    Il2CppObject* ret = il2cpp::api().runtime_invoke(m, self, pack.data(), &exc);
    if (exc) {
        return JS_ThrowInternalError(ctx, "'%s' lancou excecao no jogo",
                                     il2cpp::api().method_get_name(m));
    }
    return fromReturn(ctx, m, ret);
}

#if defined(__aarch64__)

/**
 * O `methodPointer` e o primeiro campo do MethodInfo.
 *
 * Nao e adivinhacao: esta assim no il2cpp.h que o proprio dump do jogo gera
 * (refs/il2cpp.h), antes do virtualMethodPointer e do invoker_method.
 */
void* methodPointerOf(const MethodInfo* m) {
    return *reinterpret_cast<void* const*>(m);
}

struct Cached {
    bool direct = false;
    bool isInstance = false;
    mutable bool classReady = false;   // construtor estatico da classe ja garantido
    void* fn = nullptr;
    AbiPlan plan;
};

const Cached& planFor(const MethodInfo* m) {
    // Referencia estavel: unordered_map guarda cada par no seu proprio no, e
    // nada e removido. So se chega aqui com o motor JS travado.
    static std::unordered_map<const MethodInfo*, Cached> cache;
    auto it = cache.find(m);
    if (it != cache.end()) return it->second;

    Cached c;
    // Quem responde e o runtime: deduzir de `self != nullptr` erraria num
    // getter estatico, que tambem chega com self nulo — e ai o plano poria o
    // primeiro parametro em x1 e o metodo leria lixo em x0. Uma vez so, aqui:
    // no caminho quente isto era uma chamada de API por invocacao.
    auto& api = il2cpp::api();
    c.isInstance = api.method_is_instance ? api.method_is_instance(m) : false;
    c.fn = methodPointerOf(m);
    std::string erro;
    if (!c.fn) {
        // Metodo abstrato ou so com corpo virtual: nao ha para onde saltar.
        erro = "nao tem methodPointer";
    } else {
        erro = planAbi(m, c.isInstance, &c.plan);
    }
    c.direct = erro.empty();
    if (!c.direct) {
        BL_INFO("invoke: '%s' vai pelo runtime_invoke (%s)",
                il2cpp::api().method_get_name(m), erro.c_str());
    }
    return cache.emplace(m, std::move(c)).first->second;
}

#endif // __aarch64__

} // namespace

JSValue invokeMethod(JSContext* ctx, const MethodInfo* m, void* self,
                     int argc, JSValueConst* argv) {
#if defined(__aarch64__)
    const Cached& c = planFor(m);
    if (c.direct) {
        // O runtime_invoke roda o construtor estatico da classe; o salto
        // direto nao, e o codigo do IL2CPP deixa isso para quem CHAMA. Sem
        // isto, QuickStacking['int GetCategory(int type)'] rodava com os
        // estaticos da classe ainda nulos.
        if (!c.classReady) {
            c.classReady = true;
            auto& api = il2cpp::api();
            if (api.runtime_class_init) api.runtime_class_init(api.method_get_class(m));
        }
        const AbiPlan& p = c.plan;
        intptr_t a[kIntSlots] = {0};
        uint64_t d[8] = {0};
        if (c.isInstance) a[0] = reinterpret_cast<intptr_t>(self);

        const int n = static_cast<int>(p.params.size());
        ArgScratch scratch;   // vive ate o metodo voltar (Rectangle? por endereco)
        // ref/out: o metodo recebe o endereco de uma variavel nossa, com o
        // valor do Ref; na volta, o que ele deixou la vai para o Ref.
        struct RefArg { int param; void* var; TypeDesc pointee; };
        std::vector<RefArg> refs;
        for (int i = 0; i < n; ++i) {
            JSValueConst v = (i < argc) ? argv[i] : JS_UNDEFINED;
            const ParamPlan& pp = p.params[i];
            if (pp.opaque) {
                if (!isRef(v)) {
                    return JS_ThrowTypeError(ctx, "argumento %d e ref/out (%s): passe um new Ref(valor)",
                                             i + 1, pp.d.name.c_str());
                }
                // Ref preso (recebido num hook): repassa a variavel de quem
                // chamou, sem copia — o metodo escreve direto nela.
                const TypeDesc* bound = nullptr;
                if (void* var = boundRefPtr(v, &bound)) {
                    if (bound->prim != pp.d.prim || bound->size != pp.d.size ||
                        (pp.d.prim == Prim::Struct && bound->cls != pp.d.cls)) {
                        return JS_ThrowTypeError(ctx, "argumento %d e ref/out %s, mas o Ref aponta para %s",
                                                 i + 1, pp.d.name.c_str(), bound->name.c_str());
                    }
                    a[pp.reg] = reinterpret_cast<intptr_t>(var);
                    continue;
                }
                RefArg r{i, nullptr, pp.d};
                r.pointee.byRef = false;
                const size_t sz = r.pointee.size < sizeof(void*) ? sizeof(void*) : r.pointee.size;
                r.var = scratch.take(sz);
                if (!r.var) return JS_ThrowInternalError(ctx, "sem espaco para o ref/out do argumento %d", i + 1);
                std::memset(r.var, 0, sz);
                JSValue init = refStoredValue(ctx, v);
                // `out` costuma vir de new Ref() sem valor: a variavel comeca zerada.
                const int ok = JS_IsUndefined(init) ? 1 : writeAt(ctx, r.var, r.pointee, init);
                JS_FreeValue(ctx, init);
                if (ok < 0) return JS_EXCEPTION;
                a[pp.reg] = reinterpret_cast<intptr_t>(r.var);
                refs.push_back(std::move(r));
                continue;
            }
            if (jsToParam(ctx, v, pp, a, d, &scratch) < 0) return JS_EXCEPTION;
        }
        // Todo metodo gerado pelo IL2CPP recebe o proprio MethodInfo como
        // ultimo argumento. No hook ele vem de graca nos registradores do
        // chamador; aqui somos nos o chamador.
        a[p.methodInfoReg] = reinterpret_cast<intptr_t>(m);

        bool threw = false;
        Outcome o = callRaw(c.fn, p, a, d, &threw);
        if (threw) {
            return JS_ThrowInternalError(ctx, "'%s' lancou excecao no jogo",
                                         il2cpp::api().method_get_name(m));
        }
        for (const RefArg& r : refs) {
            const TypeDesc& t = r.pointee;
            JSValue back = (t.prim == Prim::Struct && !t.nullable())
                               ? makeStructCopy(ctx, t.cls, r.var, t.size)
                               : readAt(ctx, r.var, t, JS_UNDEFINED);
            if (JS_IsException(back)) return back;
            refStore(ctx, argv[r.param], back);
        }
        return outcomeToJs(ctx, p, o);
    }
#endif
    return viaRuntimeInvoke(ctx, m, self, argc, argv);
}

} // namespace bl::script
#endif
