#pragma once
#include "script/bridge/ScriptEngine.h"
#include "il2cpp/Types.h"

namespace bl::script {
bool saveModItemData(Il2CppObject* item, std::string& data);
bool loadModItemData(Il2CppObject* item, const std::string& data);
}

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.items (itens de mod: register, isModItem) e bl.menu (catalogo do menu:
 * itemCategory, addItem). Ver content/items/ModItems.h para o que acontece no jogo.
 */
void installItemsApi(JSContext* ctx, JSValue bl);

/** O mod aparece no menu com o nome do manifesto e o icon.png (itens e NPCs chamam). */
void noteModForMenu(const std::string& mod);

} // namespace bl::script
#endif
