// As primitivas dos efeitos de tela. BlendMode tem os mesmos números das
// constantes BL_BLEND_* do `#include <bl/color>` (blBlend no GLSL).
const ScreenGraphics = Object.freeze({
    Shader: ScreenShader,
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
});
