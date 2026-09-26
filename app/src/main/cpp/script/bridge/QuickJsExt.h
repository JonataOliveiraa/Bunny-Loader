#pragma once
// O que o QuickJS nao exporta e o motor compartilhado entre threads precisa.
// Implementado em QuickJsExt.c, que compila o quickjs.c.

#include "quickjs.h"

#ifdef __cplusplus
extern "C" {
#endif

/** Onde fica o topo da pilha de frames JS em execucao (&rt->current_stack_frame). */
void **bl_js_stack_frame_slot(JSRuntime *rt);

/**
 * O topo da pilha nativa de onde o limite de pilha do motor e contado
 * (rt->stack_top), e a troca dele com o limite recalculado. O JS_UpdateStackTop
 * so sabe por o ponteiro de pilha ATUAL.
 */
uintptr_t bl_js_stack_top(JSRuntime *rt);
void bl_js_set_stack_top(JSRuntime *rt, uintptr_t top);

#ifdef __cplusplus
}
#endif
