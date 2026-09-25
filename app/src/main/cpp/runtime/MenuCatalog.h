#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl::runtime {

/**
 * O que o Mod Menu precisa saber de NPCs e buffs para as subcategorias, lido
 * do jogo junto com os nomes de item (Cheats.cpp), na thread do jogo, quando
 * o menu pede o catalogo pela primeira vez.
 *
 * Os numeros das classes sao o contrato com o CheatBridge.java.
 */

// NPC: 0 outros, 1 chefe (flag boss), 2 monstro, 3 morador, 4 criatura.
enum NpcClass : uint8_t { kNpcOther = 0, kNpcBoss = 1, kNpcMonster = 2, kNpcTown = 3, kNpcCritter = 4 };

// Buff: 1 buff, 2 debuff, 3 comida, 4 frasco, 5 pet e luz, 6 invocacao/montaria.
enum BuffClass : uint8_t {
    kBuffOther = 0, kBuffGood = 1, kBuffDebuff = 2, kBuffFood = 3, kBuffFlask = 4,
    kBuffPet = 5, kBuffSummon = 6,
};

/** Monta tudo de uma vez (sao ~700 NPCs e ~400 buffs). Thread do jogo. */
void buildMenuCatalogExtras();

// Prontos depois do buildMenuCatalogExtras (vazios se falhou).
const std::vector<uint8_t>& npcClasses();          // indice = tipo
const std::vector<std::u16string>& buffNames();    // indice = tipo; [0] vazio
const std::vector<uint8_t>& buffClasses();

} // namespace bl::runtime
