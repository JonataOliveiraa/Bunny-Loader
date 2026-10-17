#pragma once
#include <cstdint>

namespace bl::graphics {

enum class GpuChannel { Effect, Frame };

/**
 * Tempo de GPU de um trecho de comandos GL (GL_EXT_disjoint_timer_query).
 * Thread de render. O resultado chega alguns quadros depois: cada Begin
 * recolhe as consultas que ja terminaram. Sem a extensao, nao mede nada.
 *
 * Duas medidas, porque consultas de tempo nao se aninham: o efeito (Effect) e
 * o quadro inteiro de um swap ao outro (Frame), em quadros alternados. O do
 * quadro pega o que o tempo do efeito nao ve: numa GPU por blocos (Mali,
 * Adreno), trocar de framebuffer no meio do quadro parte a passada de render.
 */
class GpuTimer {
public:
    // Desligado ate as ferramentas de dev pedirem (bl.screen.__stats): no
    // Mali-G52 (Vivo Y5s), a consulta aberta de um swap ao outro fazia pedacos
    // da tela piscarem com o quadro anterior, mesmo sem efeito nenhum.
    static void Enable();
    // false se outra medida esta aberta (o trecho fica sem medida).
    static bool Begin(GpuChannel channel);
    static void End(GpuChannel channel);
    static bool Available();
    // Soma (ns) e quantidade de medidas ja recolhidas.
    static void Totals(GpuChannel channel, int64_t& totalNs, int64_t& samples);
};

} // namespace bl::graphics
