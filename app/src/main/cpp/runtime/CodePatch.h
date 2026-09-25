#pragma once
#include <cstdint>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Troca o limite de uma comparacao COMPILADA no codigo do jogo.
 *
 * Tabela nao e o unico lugar onde o Count de fabrica mora: `if (type >=
 * NPCID.Count) return;` vira `cmp wN, #696` direto na instrucao, e nao ha
 * array para aumentar. O NPC.NPCLoot sai por ai (NPC de mod nao dava drop
 * nenhum) e o Main.DrawNPCs pula o NPC (ficava invisivel).
 *
 * Procura, DENTRO do metodo `m` (do methodPointer ate o proximo metodo da
 * mesma classe), toda instrucao `cmp wN, #oldLimit` — o SUBS WZR, Wn, #imm12
 * do AArch64 — seguida de um desvio de ORDEM (b.hi/b.ls/b.gt/b.le), e troca
 * o imediato por `newLimit`. Desvio de igualdade fica: e `if (type == 696)`,
 * um NPC especifico, e nao limite. O metodo e achado pelo NOME, e o padrao tem de casar: se uma
 * versao nova do jogo mudar o codigo, nada e escrito e quem chama fica sabendo
 * pelo retorno (quantas instrucoes trocou).
 *
 * `belowToo` aceita tambem b.lo/b.hs/b.lt/b.ge (`tipo < Count`): so para
 * metodo cujo codigo foi conferido (o GUIBuffs.Draw usa as duas formas).
 *
 * `newLimit` cabe em 12 bits (ate 4095). `shifted`: o `cmp wN, #imm, lsl #12`
 * (limite imm * 4096; o TileData compara assim a chave tipo << 12). Chamar na
 * thread do jogo, uma vez.
 */
int patchCompareLimit(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit, bool belowToo = false,
                      bool shifted = false);

} // namespace bl::runtime
