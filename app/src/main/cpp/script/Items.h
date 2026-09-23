#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.items (itens de mod: register, isModItem) e bl.menu (catalogo do menu:
 * itemCategory, addItem). Ver runtime/ModItems.h para o que acontece no jogo.
 */
void installItemsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
