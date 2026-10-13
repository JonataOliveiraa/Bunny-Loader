// Cores para os efeitos: tudo vira [r, g, b, a] de 0 a 1, o que o GLSL usa.
// Aceita Color do jogo (R, G, B, A de 0 a 255), [r, g, b(, a)] de 0 a 1 e
// '#rrggbb' / '#rrggbbaa'.
const ColorMath = Object.freeze({
    ToFloats(color) {
        if (color === undefined || color === null) return null;
        if (typeof color === 'string') return ColorMath.FromHex(color);
        if (Array.isArray(color)) return [color[0], color[1], color[2], color[3] ?? 1].map(Number);
        if (typeof color.R === 'number') return [color.R / 255, color.G / 255, color.B / 255, (color.A ?? 255) / 255];
        return null;
    },

    FromHex(hex) {
        const h = String(hex).replace(/^#/, '');
        if (!/^([0-9a-f]{6}|[0-9a-f]{8})$/i.test(h)) throw new TypeError('ColorMath.FromHex: expected #rrggbb or #rrggbbaa');

        const byte = (i) => parseInt(h.slice(i, i + 2), 16) / 255;
        return [byte(0), byte(2), byte(4), h.length === 8 ? byte(6) : 1];
    },

    // h em graus, s e v de 0 a 1.
    Hsv(h, s, v, a = 1) {
        const hue = (((h % 360) + 360) % 360) / 60;
        const f = (n) => {
            const k = (n + hue) % 6;
            return v - v * s * Math.max(Math.min(k, 4 - k, 1), 0);
        };
        return [f(5), f(3), f(1), a];
    },

    Lerp(a, b, t) {
        const x = ColorMath.ToFloats(a), y = ColorMath.ToFloats(b);
        return x.map((v, i) => v + (y[i] - v) * t);
    },

    // A cor de uma luz pela temperatura (1000 K vermelha, 6500 K branca,
    // 12000 K azulada).
    Temperature(kelvin) {
        const t = Math.min(Math.max(kelvin, 1000), 40000) / 100;
        const clamp = (v) => Math.min(Math.max(v, 0), 255) / 255;
        const r = t <= 66 ? 255 : 329.698727446 * Math.pow(t - 60, -0.1332047592);
        const g = t <= 66 ? 99.4708025861 * Math.log(t) - 161.1195681661 : 288.1221695283 * Math.pow(t - 60, -0.0755148492);
        const b = t >= 66 ? 255 : t <= 19 ? 0 : 138.5177312231 * Math.log(t - 10) - 305.0447927307;
        return [clamp(r), clamp(g), clamp(b), 1];
    },

    Luma(color) {
        const c = ColorMath.ToFloats(color);
        return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    },
});
