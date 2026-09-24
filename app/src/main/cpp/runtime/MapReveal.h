#pragma once
#include "il2cpp/Api.h"

#include <chrono>

namespace bl::runtime {

/**
 * Revela colunas inteiras do mapa, de `x` ate o tempo acabar ou chegar em
 * `maxX`, com WorldMap.UpdateLighting(x, y, 255) — o que a luz faz quando o
 * jogador passa. Devolve a proxima coluna.
 *
 * Arquivo proprio porque e compilado com -fexceptions (ver CMakeLists): o
 * UpdateLighting lanca NullReference quando o mapa nao consegue a memoria de um
 * pedaco, e sem tabela de desenrolar aqui isso seria std::terminate. `threw`
 * vira true e a revelacao para.
 */
int revealMapColumns(Il2CppObject* map, const MethodInfo* updateLighting, int x, int maxX,
                     int maxY, std::chrono::steady_clock::time_point until, bool* threw);

} // namespace bl::runtime
