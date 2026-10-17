#pragma once
#include "graphics/RenderHook.h"
#include <string>

namespace bl::graphics {

/**
 * Hooks na libEGL/libGLESv2, na thread de render da Unity (UnityGfxDeviceW).
 *
 * Esta libunity nao tem mais GL.IssuePluginEvent nem CommandBuffer (o build
 * removeu as icalls que o jogo nao usa), entao o ponto do quadro tem de ser
 * achado nas proprias chamadas GL. Por enquanto: o eglSwapBuffers (fim do
 * quadro, onde a prova de conceito desenha) e um rastro de um quadro inteiro
 * (framebuffers, viewports, clears, desenhos) para achar a fronteira entre a
 * camera do mundo e a da interface.
 */
void installGlHooks();   // uma vez so; as chamadas seguintes nao fazem nada

// Grava o proximo quadro inteiro; o resultado sai no log ("rastro GL: ...").
void requestGlTrace();

// Os contadores do espelho do estado GL (GlShadow.h).
void fillGlHookStats(RenderStats& stats);

} // namespace bl::graphics
