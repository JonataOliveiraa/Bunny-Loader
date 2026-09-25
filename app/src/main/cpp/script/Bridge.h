#pragma once
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

#include <cstdint>
#include <string>
#endif

namespace bl::script {

#if BL_HAVE_QUICKJS

// Fabricas dos objetos JS que embrulham coisas do jogo. Ficam em Bindings.cpp,
// que detem os JSClassID; Value.cpp e JsHook.cpp chamam por aqui.
JSValue makeNativeObject(JSContext* ctx, Il2CppObject* obj);
JSValue makeGameArray(JSContext* ctx, Il2CppArray* arr);
JSValue makeGameMethod(JSContext* ctx, const MethodInfo* m);

// Inverso: o valor JS e um objeto/array do jogo? nullptr se nao for.
Il2CppObject* objectFromJS(JSValueConst v);
Il2CppArray* arrayFromJS(JSValueConst v);
/** A classe de um valor como `Terraria.Item`; nullptr se nao for classe. */
Il2CppClass* classFromJS(JSValueConst v);
/** O objeto e um array (de qualquer tipo de elemento)? */
bool isArrayObject(Il2CppObject* o);

/**
 * Filtro NATIVO de um hook JS: so vai ao JS a chamada em que o objeto `on`
 * (o self, ou o parametro de indice `on`) tem o campo int `field` >= minType.
 * As outras vao direto ao original, sem trava nem JS — o NPC.AI roda por
 * NPC por quadro, e so os de mod interessam ao mod.
 */
struct HookFilter {
    int on = -2;              // -2 = sem filtro, -1 = self, >= 0 = parametro
    std::string field = "type";
    int32_t minType = 0;
    // So chama o JS enquanto esta thread esta dentro do hook JS deste outro
    // metodo (que precisa ja ter um). Ex.: SpriteBatch.DrawString so durante
    // o desenho do tooltip, sem pagar o JS nos outros milhares de textos.
    const MethodInfo* whileIn = nullptr;
};

// Instala um hook JS num metodo do jogo. O callback recebe
// (original, self, ...args). Retorna false (com excecao posta no ctx) se nao
// houver slot livre, se o hook nativo falhar ou se a assinatura do metodo nao
// couber na convencao de chamada que sabemos reproduzir. Ver JsHook.cpp.
bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback, const HookFilter* filter = nullptr);

#endif

} // namespace bl::script
