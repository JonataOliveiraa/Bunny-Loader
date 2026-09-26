#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * Arquivos para os mods: bl.file, bl.directory, bl.path, bl.mod e bl.info.
 *
 * Caminho relativo e relativo a pasta do main.js do mod que chama (como o
 * bl.loadTexture); absoluto vale como esta. `bl.mod.dataDirectory` e a
 * pasta de dados do mod, fora do pacote: sobrevive a atualizar o mod.
 */
void installFilesApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
