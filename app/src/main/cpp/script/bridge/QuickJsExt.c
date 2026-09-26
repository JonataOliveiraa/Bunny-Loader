// O quickjs.c do quickjs-ng (third_party/quickjs, nao versionado) e compilado
// POR AQUI, com o que so ele pode ter: o JSRuntime e opaco fora dele. Assim o
// clone fica intacto e nada se perde ao clonar de novo; se um dia o campo
// mudar de nome, o build quebra aqui, e nao o motor em silencio.
#include "quickjs.c"

#include "script/bridge/QuickJsExt.h"

// Onde o runtime guarda o topo da pilha de frames JS em execucao. Ver
// JsSuspend (ScriptEngine.h): cada thread guarda o seu ao soltar o motor e o
// devolve ao pegar de volta; pelo endereco, sem uma chamada a cada vez.
void **bl_js_stack_frame_slot(JSRuntime *rt)
{
    return (void **)&rt->current_stack_frame;
}

// O mesmo para o limite de pilha: quem solta o motor no meio de um callback
// (JsSuspend) volta com o topo da SUA thread de quando entrou, e nao com o
// ponteiro de pilha de agora, que esta mais fundo e daria folga nova a cada
// original() aninhado.
uintptr_t bl_js_stack_top(JSRuntime *rt)
{
    return rt->stack_top;
}

void bl_js_set_stack_top(JSRuntime *rt, uintptr_t top)
{
    rt->stack_top = top;
    update_stack_limit(rt);
}
