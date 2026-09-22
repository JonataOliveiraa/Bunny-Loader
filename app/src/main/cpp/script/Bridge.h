#pragma once
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

namespace bl::script {

#if BL_HAVE_QUICKJS

// Cria um NativeObject JS envolvendo um Il2CppObject* (definido em Bindings.cpp,
// que detem o JSClassID). Usado pelo dispatcher de hook para entregar o `self`.
JSValue makeNativeObject(JSContext* ctx, Il2CppObject* obj);

// Inverso: o valor JS e um objeto do jogo? nullptr se nao for.
Il2CppObject* objectFromJS(JSValueConst v);

// Instala um hook JS num metodo do jogo. O callback recebe
// (original, self, ...args) como no TL Pro. Retorna false se nao houver slot
// livre ou o hook nativo falhar. Definido em JsHook.cpp.
bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback);

#endif

} // namespace bl::script
