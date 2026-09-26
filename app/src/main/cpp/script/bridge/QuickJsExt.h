#pragma once
// O que o QuickJS nao exporta e o motor compartilhado entre threads precisa.
// Implementado em QuickJsExt.c, que compila o quickjs.c.

#include "quickjs.h"

#ifdef __cplusplus
extern "C" {
#endif

/** Onde fica o topo da pilha de frames JS em execucao (&rt->current_stack_frame). */
void **bl_js_stack_frame_slot(JSRuntime *rt);

#ifdef __cplusplus
}
#endif
