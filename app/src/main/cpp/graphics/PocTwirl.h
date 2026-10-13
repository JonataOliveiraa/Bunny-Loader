#pragma once
#include "graphics/GlShadow.h"
#include "graphics/RenderHook.h"

namespace bl::graphics {

// Onde desenhar: o framebuffer da camera do mundo e o viewport dela.
struct DrawTarget {
    int fbo = 0;
    int viewport[4] = {0, 0, 0, 0};
};

/**
 * O efeito da prova de conceito: copia o que a camera do mundo desenhou e
 * redesenha um redemoinho em volta de um ponto. Thread de render.
 */
class PocTwirl {
public:
    // `state`: o estado da Unity naquele ponto (GlShadow.h), devolvido no fim.
    static void Draw(const PocParams& params, const DrawTarget& target, const GlShadow& state);
    static void FillStats(RenderStats& stats);
};

} // namespace bl::graphics
