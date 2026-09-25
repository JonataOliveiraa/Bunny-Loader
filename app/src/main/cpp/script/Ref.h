#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "script/Value.h"

namespace bl::script {

/**
 * `ref`/`out` do C# no JS: um objeto `Ref` com `.value`.
 *
 * Num HOOK, o parametro ref/out chega como Ref PRESO ao endereco do jogo: ler
 * `.value` le a variavel do chamador, escrever altera a variavel dele — e o
 * `ref int dano` do tModLoader. Quando o callback volta, o Ref se solta: guarda
 * o ultimo valor e para de tocar a memoria (o endereco era da pilha do jogo).
 *
 * Numa CHAMADA do mod, o parametro ref/out recebe um Ref solto
 * (`new Ref(valor)`): a ponte da ao metodo uma variavel com esse valor e, na
 * volta, poe no Ref o que o metodo deixou la. Um Ref ainda preso (repassado
 * de dentro do hook) vai como o endereco da variavel de quem chamou.
 *
 * Struct por ref (`ref FishingAttempt`) sai como COPIA a cada leitura:
 * `const a = r.value; a.crate = true; r.value = a;`. Uma vista direta ficaria
 * apontando para a pilha depois do hook.
 */

/** Registra a classe e o construtor global `Ref`. Uma vez, no installBindings. */
void installRefClass(JSContext* ctx, JSValueConst global);

/** Ref preso a `ptr`, que guarda um valor do tipo `pointee` (sem o `&`). */
JSValue makeBoundRef(JSContext* ctx, void* ptr, const TypeDesc& pointee);

/** Solta um Ref preso: guarda o valor atual e esquece o endereco. */
void unbindRef(JSContext* ctx, JSValueConst ref);

/** E um Ref? */
bool isRef(JSValueConst v);

/**
 * Endereco de um Ref PRESO (dentro do hook), ou nullptr se solto. `type` sai
 * com o tipo da variavel. Serve para repassar o `ref` adiante numa chamada,
 * como em C#: o metodo de dentro escreve direto na variavel de quem chamou.
 */
void* boundRefPtr(JSValueConst ref, const TypeDesc** type);

/** O valor guardado de um Ref solto (nova referencia). */
JSValue refStoredValue(JSContext* ctx, JSValueConst ref);

/** Troca o valor guardado de um Ref solto. Consome `v`. */
void refStore(JSContext* ctx, JSValueConst ref, JSValue v);

} // namespace bl::script
#endif
