#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "il2cpp/Types.h"

namespace bl::script {

/**
 * Chama um metodo do jogo com argumentos vindos do JS.
 *
 * Prefere a CHAMADA DIRETA: monta os registradores e entra pelo
 * `methodPointer`, que e o mesmo endereco que o jogo usa. O `runtime_invoke`
 * faz duas coisas caras em toda chamada que a direta nao faz — monta um vetor
 * de ponteiros para os argumentos e ENCAIXOTA o retorno (uma alocacao no heap
 * do coletor por chamada, mesmo para devolver um int).
 *
 * Nao troca a semantica: o `runtime_invoke` tambem chama pelo `methodPointer`,
 * sem despacho virtual, entao os dois caminhos entram no mesmo lugar — e num
 * metodo hookado, nos dois casos, quem atende e o hook.
 *
 * Cai de volta no `runtime_invoke` sempre que o plano da ABI nao fecha
 * (argumentos demais para os registradores, `ref`/`out`, struct de retorno
 * grande demais). Nunca recusa por causa disso.
 */
JSValue invokeMethod(JSContext* ctx, const MethodInfo* m, void* self,
                     int argc, JSValueConst* argv);

} // namespace bl::script
#endif
