#pragma once

namespace bl::runtime {

// Valida a camada de RESOLUCAO sem depender de inline hook.
//
// O ShadowHook nao opera sob o houdini (tradução ARM->x86 do MuMu), entao o
// hook de il2cpp_init nao dispara no emulador. Mas as chamadas a API do IL2CPP
// (il2cpp_*) sao chamadas de funcao normais, que o houdini traduz — logo dá
// para achar classes/campos/metodos e ler estado do jogo mesmo aqui.
//
// startResolutionProbe() sobe uma thread que espera o il2cpp_init terminar
// (poll em il2cpp_domain_get) e entao roda Api::load() + resolveGameRefs(),
// logando o resultado. Em ARM real o caminho do hook faz o mesmo mais cedo;
// resolveGameRefs e idempotente, entao os dois convivem.
void startResolutionProbe();

} // namespace bl::runtime
