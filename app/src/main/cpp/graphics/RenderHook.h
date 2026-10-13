#pragma once
#include "graphics/EffectTypes.h"
#include <cstdint>

namespace bl::graphics {

/**
 * O ponto do quadro em que os efeitos de tela desenham.
 *
 * O porte XNA->Unity desenha o mundo e a interface com duas cameras
 * (XNAUnityRunner.WorldCameraSettings.Camera e UICameraSettings.Camera); um
 * hook em Camera.FireOnPostRender ve cada uma terminar, na thread do jogo. O
 * GL roda em outra thread (UnityGfxDeviceW, render multithread) e esta
 * libunity nao tem GL.IssuePluginEvent: o desenho vai pelos hooks GL
 * (graphics/GlHooks.h), achado por um marcador.
 */
void installRenderHook();

// O marcador: um Graphics.Blit para uma RenderTexture deste tamanho, emitido
// quando a camera do mundo termina. Na thread de render ele aparece como
// glViewport(0, 0, 17, 3), na posicao certa da fila.
constexpr int kMarkerWidth = 17;
constexpr int kMarkerHeight = 3;

// Thread do jogo, a cada quadro (bl.screen.submit): os passos do quadro que
// vai ser desenhado. Sem submit no quadro, nada desenha.
void submitEffects(EffectFrame&& frame);

// Thread de render (GlHooks.cpp): o quadro do marcador (depois do mundo) e o
// do fim do quadro (por cima da interface). false se nao ha.
bool takeWorldEffects(EffectFrame& out);
bool takeScreenEffects(EffectFrame& out);
void noteMarkerHit();

struct RenderStats {
    int64_t worldPostRender = 0, uiPostRender = 0, otherPostRender = 0;
    int64_t draws = 0, frames = 0, markersIssued = 0, markerHits = 0, screenFrames = 0;
    int64_t renderCpuNs = 0, markerCpuNs = 0, gpuNs = 0, gpuSamples = 0;   // somas desde o boot
    int64_t gpuFrameNs = 0, gpuFrameSamples = 0;                           // quadro inteiro, alternados
    int64_t shadowChecks = 0, shadowMismatches = 0;                        // espelho do estado GL
    int64_t boxPixels = 0;                                                 // pixels copiados e desenhados
    int32_t drawFbo = -1, readFbo = -1, viewport[4] = {0, 0, 0, 0};
    int32_t samples = -1, blitError = 0, copyFallbacks = 0;
    bool hookInstalled = false, hasContext = false, markerReady = false, gpuTimer = false;
};

RenderStats renderStats();

} // namespace bl::graphics
