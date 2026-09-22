#pragma once

namespace bl::ui {

// Instala um botao flutuante ("Minishark") na Activity do jogo, de dentro do
// processo do jogo, via JNI. Sem root, sem app separado, sem permissao de
// overlay. O onClick chama bl::runtime::requestGive(98); o hook de DoUpdate
// executa o spawn na thread do jogo. No-op (logando) se o JNI/Activity/dex
// nao estiverem disponiveis. Deve ser chamada depois do jogo assentar.
void installCheatButton();

} // namespace bl::ui
