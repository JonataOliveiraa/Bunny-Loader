#pragma once
#include <cstdint>
#include <string>

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

/**
 * Por que o patchCompareLimit de `m` nao trocou nada, em texto para o log.
 *
 * "Nao achado" escondia causas diferentes: o padrao nao esta no metodo (outra
 * versao do jogo), ja foi trocado antes (instalacao repetida) ou esta la e a
 * escrita foi recusada (mprotect). So le; chamar depois do patch que falhou,
 * com os mesmos argumentos. Termina com describeMethodCode.
 */
std::string describeCompareMiss(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit,
                                bool belowToo = false, bool shifted = false);

/**
 * Onde o metodo esta (`libil2cpp.so+0x165c6a4`), o tamanho que a busca
 * considera e as 4 primeiras instrucoes — um hook na entrada aparece ai.
 */
std::string describeMethodCode(const MethodInfo* m);

/**
 * O fim de um laco que anda pelo array em BYTES: o IL2CPP compila
 * `for (i = 0; i < 697; i++) a[i]` como `x = 32; ...; x++; cmp x, #729; b.ne`
 * (32 = o cabecalho do array). Troca o `cmp xN, #oldEnd` (64 bits) seguido de
 * b.ne/b.lt/b.lo. A scan_limits.py nao acha essa forma; a scan_loops.py
 * (tools/disasm) acha. Chamar na thread do jogo, uma vez.
 */
int patchLoopEnd(const MethodInfo* m, uint32_t oldEnd, uint32_t newEnd);

/**
 * O `0 < x < N` com N PAR, que o clang compila pela metade:
 * `sub wA, wX, #1; lsr wB, wA, #1; cmp wB, #(N-2)/2; b.hi` — aceita x de 1 a
 * 2*imm+2. O `itemId < ItemID.Count` (6147) dos CommonCode.DropItem* e assim
 * (imm 0xC00), e a scan_limits.py nao acha; a scan_halved.py (tools/disasm)
 * acha. Troca o imediato do `cmp` que vem logo depois de um `lsr #1` no mesmo
 * registrador e antes de um b.hi. Chamar na thread do jogo.
 */
int patchHalvedLimit(const MethodInfo* m, uint32_t oldImm, uint32_t newImm);

/**
 * O tamanho de um `new T[697]`: `mov wN, #697` antes do il2cpp_array_new.
 * Troca TODO `movz wN, #oldValue` do metodo: so para metodo conferido, em que
 * o numero so aparece ali.
 */
int patchMovImmediate(const MethodInfo* m, uint32_t oldValue, uint32_t newValue);

} // namespace bl::runtime
