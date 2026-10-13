#pragma once
#include "graphics/EffectTypes.h"
#include "graphics/GlShadow.h"
#include "graphics/RenderHook.h"

namespace bl::graphics {

// Onde desenhar: o framebuffer da camera do mundo e o viewport dela.
struct DrawTarget {
    int fbo = 0;
    int viewport[4] = {0, 0, 0, 0};
};

/**
 * Executa os passos de um quadro na thread de render.
 *
 * Um passo desenha a caixa dele na tela ou num alvo (EffectDraw.dest) com o
 * shader dele. O que o shader le da tela (uScreen) vem de uma copia do que ja
 * esta desenhado, inclusive os passos de antes: so e copiado quando o shader
 * usa uScreen e a tela mudou desde a ultima copia. A mistura com a tela
 * (BlendMode) e feita no shader, sem blend do GL.
 *
 * Toda textura (a copia, o quadro anterior, os alvos) cobre o viewport
 * inteiro, entao o mesmo vUV le o mesmo lugar em qualquer uma.
 */
class EffectRenderer {
public:
    // `state`: o estado da Unity naquele ponto (GlShadow.h), devolvido no fim.
    static void Execute(const EffectFrame& frame, const DrawTarget& target, const GlShadow& state);
    static void FillStats(RenderStats& stats);
};

} // namespace bl::graphics
