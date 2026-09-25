#pragma once
#include "il2cpp/Types.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

namespace bl::script {

#if BL_HAVE_QUICKJS

/**
 * Campos que o mod poe em classes do jogo: `bl.defineField(Terraria.Item,
 * 'ModItem')` e depois `item.ModItem = x` / `item.ModItem`.
 *
 * Campo de verdade nao da: no IL2CPP o tamanho do objeto e a posicao de cada
 * campo estao gravados no binario, e todo o codigo compilado le por posicao
 * fixa. Entao o valor mora numa tabela ao lado, indexada pelo objeto, com
 * referencia FRACA a ele (il2cpp_gchandle_new_weakref): a tabela nao segura o
 * objeto vivo, e quando o coletor o recolhe a entrada e descartada. O coletor
 * (Boehm) nao move objetos, entao o endereco serve de chave enquanto o objeto
 * vive; a referencia fraca e o que distingue o objeto de um novo que venha a
 * nascer no mesmo endereco.
 *
 * Tudo com o motor travado (JsLock), como o resto da ponte.
 */

/** O nome e um campo definido para `cls` (ou uma classe base)? */
bool isExtraField(Il2CppClass* cls, JSAtom atom);

/** Le o campo: undefined se o objeto nunca recebeu valor. */
JSValue extraFieldGet(JSContext* ctx, Il2CppObject* obj, JSAtom atom);

/** Grava o campo. @return 1 ok, -1 com excecao posta. */
int extraFieldSet(JSContext* ctx, Il2CppObject* obj, JSAtom atom, JSValueConst value);

/** bl.defineField, bl.addressOf, bl.objectAt. */
void installExtraFields(JSContext* ctx, JSValueConst bl);

#endif

} // namespace bl::script
