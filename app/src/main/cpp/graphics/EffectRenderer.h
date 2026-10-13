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
 * Executa os efeitos de um quadro na thread de render. Cada efeito copia a
 * propria caixa do framebuffer (o que ja esta desenhado, inclusive os efeitos
 * de antes nele) para a textura uScreen e redesenha a caixa com o shader dele:
 * a mistura com a tela (BlendMode) e feita no shader, sem blend do GL.
 */
class EffectRenderer {
public:
    // `state`: o estado da Unity naquele ponto (GlShadow.h), devolvido no fim.
    static void Execute(const EffectList& effects, const DrawTarget& target, const GlShadow& state);
    static void FillStats(RenderStats& stats);
};

} // namespace bl::graphics
