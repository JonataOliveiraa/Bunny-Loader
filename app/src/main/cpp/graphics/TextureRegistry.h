#pragma once
#include "graphics/GlApi.h"

#include <string>

namespace bl::graphics {

// As texturas dos mods tem numeros a partir daqui para baixo (-1000, -1001...),
// fora dos alvos (> 0) e das fontes fixas (kTexScreen, kTexHistory).
constexpr int kFirstModTexture = -1000;

struct LoadedTexture {
    GLuint texture = 0;
    GLuint sampler = 0;
    GLint width = 0, height = 0;
};

/**
 * Os PNG dos mods para os efeitos (ScreenGraphics.Texture). Decodificados na
 * thread do jogo pelo Android (dev.bunnyloader.game.ModImages, por JNI), com
 * as linhas invertidas para o GL (a de baixo primeiro: v = 0 e o pe da
 * imagem, como o vUV); enviados ao GL na thread de render, no primeiro uso.
 */
class TextureRegistry {
public:
    // Thread do jogo. O mesmo arquivo com o mesmo filtro devolve o mesmo
    // numero. 0 se nao deu (o motivo vai para o log).
    static int Load(const std::string& path, bool linear, bool repeat, int* width, int* height);

    // Thread de render: a textura pronta (envia na primeira vez), ou nullptr.
    static const LoadedTexture* Use(int id);

    // Grava a LUT neutra em tira (ModImages.saveNeutralLut).
    static bool SaveNeutralLut(const std::string& path, int size);
};

} // namespace bl::graphics
