// Passos prontos para o cmd.Blit: leem o uSource e escrevem no destino.
//   Copy       a fonte como está (reduzir para meia resolução, por exemplo)
//   Threshold  só o que passa de uThreshold de brilho (uKnee suaviza)
//   BlurH/V    desfoque gaussiano em uma direção; uBlurRadius em texels da fonte
// Compilam no primeiro uso.
class ScreenPasses {
    static #cache = new Map();

    static #Get(name, source) {
        if (!ScreenPasses.#cache.has(name)) ScreenPasses.#cache.set(name, ScreenShader.FromSource(source, 'bl/' + name));
        return ScreenPasses.#cache.get(name);
    }

    static #Blur(axis) {
        return `
uniform float uBlurRadius;

void main() {
    vec2 stride = ${axis} * max(uBlurRadius, 0.0) / 4.0;
    vec4 sum = blSource(vUV) * 0.2270270270;
    sum += (blSource(vUV + stride * 1.3846153846) + blSource(vUV - stride * 1.3846153846)) * 0.3162162162;
    sum += (blSource(vUV + stride * 3.2307692308) + blSource(vUV - stride * 3.2307692308)) * 0.0702702703;
    fragColor = sum;
}
`;
    }

    static get Copy() {
        return ScreenPasses.#Get('Copy', 'void main() {\n    fragColor = blSource(vUV);\n}\n');
    }

    static get Threshold() {
        return ScreenPasses.#Get('Threshold', `
#include <bl/color>
uniform float uThreshold;
uniform float uKnee;

void main() {
    vec4 c = blSource(vUV);
    float keep = smoothstep(uThreshold, uThreshold + max(uKnee, 1e-4), blLuma(c.rgb));
    fragColor = vec4(c.rgb * keep, 1.0);
}
`);
    }

    static get BlurH() {
        return ScreenPasses.#Get('BlurH', ScreenPasses.#Blur('vec2(uSourceTexel.x, 0.0)'));
    }

    static get BlurV() {
        return ScreenPasses.#Get('BlurV', ScreenPasses.#Blur('vec2(0.0, uSourceTexel.y)'));
    }
}
