// As primitivas dos efeitos de tela. BlendMode tem os mesmos números das
// constantes BL_BLEND_* do `#include <bl/color>` (blBlend no GLSL).
const ScreenGraphics = Object.freeze({
    Shader: ScreenShader,
    Passes: ScreenPasses,
    // As texturas fixas: o que já está desenhado e o quadro anterior (o mundo
    // com os efeitos, guardado só nos quadros em que algum efeito o lê).
    Sources: Object.freeze({
        Screen: ScreenTarget.Screen,
        History: ScreenTarget.History,
    }),
    BlendMode: Object.freeze({
        Normal: 0,
        Multiply: 1,
        Screen: 2,
        Overlay: 3,
        SoftLight: 4,
        Add: 5,
        Color: 6,
        Luminosity: 7,
    }),
    // Um array de vetores para um uniform (`uniform vec4 uPontos[8];`):
    // ScreenGraphics.Array(4, [x, y, z, w, x, y, z, w, ...]).
    Array: (components, values) => ({ components, values: Array.from(values, Number) }),
});
