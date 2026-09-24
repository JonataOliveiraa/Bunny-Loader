#include "script/Invoke.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "script/Marshal.h"
#include "script/Value.h"

#if defined(__aarch64__)
#include "script/Abi.h"
#endif

#include <unordered_map>

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
    if (erro.empty()) {
        // `ref`/`out` vira parametro opaco: no hook ele e repassado como
        // chegou, mas numa chamada NOSSA nao ha valor de onde tira-lo. O
        // ArgPack ja recusa com uma frase que o modder entende; deixamos o
        // caminho lento dar a mensagem em vez de inventarmos outra.
        for (const ParamPlan& p : c.plan.params) {
            if (p.opaque) { erro = "tem parametro ref/out"; break; }
        }
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
        intptr_t a[8] = {0};
        uint64_t d[8] = {0};
        if (c.isInstance) a[0] = reinterpret_cast<intptr_t>(self);

        const int n = static_cast<int>(p.params.size());
        ArgScratch scratch;   // vive ate o metodo voltar (Rectangle? por endereco)
        for (int i = 0; i < n; ++i) {
            JSValueConst v = (i < argc) ? argv[i] : JS_UNDEFINED;
            if (jsToParam(ctx, v, p.params[i], a, d, &scratch) < 0) return JS_EXCEPTION;
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
        return outcomeToJs(ctx, p, o);
    }
#endif
    return viaRuntimeInvoke(ctx, m, self, argc, argv);
}

} // namespace bl::script
#endif
