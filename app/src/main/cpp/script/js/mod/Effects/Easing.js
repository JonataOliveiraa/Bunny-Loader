// As curvas de easings.net: t de 0 a 1 -> 0 a 1 (Back e Elastic passam um
// pouco das pontas).
const Easing = Object.freeze({
    Linear: (t) => t,

    InSine: (t) => 1 - Math.cos((t * Math.PI) / 2),
    OutSine: (t) => Math.sin((t * Math.PI) / 2),
    InOutSine: (t) => -(Math.cos(Math.PI * t) - 1) / 2,

    InQuad: (t) => t * t,
    OutQuad: (t) => 1 - (1 - t) * (1 - t),
    InOutQuad: (t) => (t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2),

    InCubic: (t) => t * t * t,
    OutCubic: (t) => 1 - Math.pow(1 - t, 3),
    InOutCubic: (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2),

    InExpo: (t) => (t === 0 ? 0 : Math.pow(2, 10 * t - 10)),
    OutExpo: (t) => (t === 1 ? 1 : 1 - Math.pow(2, -10 * t)),
    InOutExpo: (t) => (t === 0 ? 0 : t === 1 ? 1 : t < 0.5 ? Math.pow(2, 20 * t - 10) / 2 : (2 - Math.pow(2, -20 * t + 10)) / 2),

    InBack: (t) => 2.70158 * t * t * t - 1.70158 * t * t,
    OutBack: (t) => 1 + 2.70158 * Math.pow(t - 1, 3) + 1.70158 * Math.pow(t - 1, 2),

    OutElastic: (t) => (t === 0 ? 0 : t === 1 ? 1 : Math.pow(2, -10 * t) * Math.sin((t * 10 - 0.75) * ((2 * Math.PI) / 3)) + 1),

    OutBounce: (t) => {
        const n = 7.5625, d = 2.75;
        if (t < 1 / d) return n * t * t;
        if (t < 2 / d) return n * (t -= 1.5 / d) * t + 0.75;
        if (t < 2.5 / d) return n * (t -= 2.25 / d) * t + 0.9375;
        return n * (t -= 2.625 / d) * t + 0.984375;
    },
});
