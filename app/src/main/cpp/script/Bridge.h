#pragma once
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
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
/** O objeto e um array (de qualquer tipo de elemento)? */
bool isArrayObject(Il2CppObject* o);

// Instala um hook JS num metodo do jogo. O callback recebe
// (original, self, ...args). Retorna false (com excecao posta no ctx) se nao
// houver slot livre, se o hook nativo falhar ou se a assinatura do metodo nao
// couber na convencao de chamada que sabemos reproduzir. Ver JsHook.cpp.
bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback);

#endif

} // namespace bl::script
