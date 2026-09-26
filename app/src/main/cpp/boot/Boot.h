#pragma once

namespace bl::runtime {

/**
 * A thread do LACO do jogo, onde Update e Draw rodam.
 *
 * Existe porque a Unity ABORTA o processo — nao lanca excecao, aborta dentro da
 * libunity — quando se cria uma textura fora dela. Os mods carregam na thread
 * da sonda, entao sem esta verificacao um `bl.loadTexture` no topo de um mod
 * derruba o jogo sem dizer por que.
 *
 * Vem do hook de DoUpdate, nao do il2cpp_init: medido, sao threads
 * DIFERENTES neste hospedeiro, e a que importa e a que roda o jogo.
 * 0 = ainda nao vista.
 */
int gameThreadId();
void noteGameThread();

// Roda logo depois do il2cpp_init, na thread principal da Unity.
void boot();

} // namespace bl::runtime
