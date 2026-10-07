#pragma once
#include <string>
#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Save dos itens de mod.
 *
 * O Player.LoadPlayer troca por nada todo tipo acima de ItemID.Count, entao o
 * item de mod sumia ao sair e voltar ao mundo. Do .plr nao da para depender:
 * ele guarda o NUMERO do tipo, e o numero muda com os mods instalados (e o
 * primeiro depois dos do jogo, na ordem de registro).
 *
 * Ao lado de cada personagem vai um `<arquivo>.plr.bl`, em texto, com onde
 * estava cada item de mod e o NOME dele ("<uid>/<nome>"):
 *   - depois que o jogo grava o .plr, anota os itens de mod do jogador;
 *   - depois que o jogo carrega um .plr, os repoe no mesmo lugar.
 * Item de mod que nao esta carregado agora (mod desligado, removido) nao some:
 * fica guardado e volta a ser escrito no proximo save.
 *
 * Os baus usam `<mundo>.wld.bl`. Dados de SaveData acompanham cada registro;
 * itens no chao continuam seguindo o ciclo normal do Terraria (nao sao
 * persistidos pelo save do mundo).
 */
void installModItemSave();
std::string modChestItemData(Il2CppObject* chest, int slot);
bool setModChestItemData(Il2CppObject* chest, int slot, const std::string& data);

} // namespace bl::runtime
