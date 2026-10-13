// Uma curva por pontos [t, valor] (t de 0 a 1, em ordem), para controlar um
// parâmetro ao longo da vida do efeito.
//   const pulse = new ScreenUtils.Curve([[0, 0], [0.2, 1], [1, 0]], { easing: Easing.InOutSine });
//   Update() { this.Intensity = pulse.At(this.Progress); }
class ScreenCurve {
    #points;
    #easing;

    // easing: entre um ponto e o próximo (Linear se não der).
    constructor(points, options = {}) {
        if (!Array.isArray(points) || points.length === 0) throw new TypeError('Curve: pass [[t, value], ...]');

        this.#points = points.map(([t, v]) => [Number(t), Number(v)]).sort((a, b) => a[0] - b[0]);
        this.#easing = options.easing ?? Easing.Linear;
    }

    At(t) {
        const points = this.#points;
        if (t <= points[0][0]) return points[0][1];

        for (let i = 1; i < points.length; i++) {
            const [t1, v1] = points[i];
            if (t > t1) continue;
            const [t0, v0] = points[i - 1];
            const local = t1 > t0 ? (t - t0) / (t1 - t0) : 1;
            return v0 + (v1 - v0) * this.#easing(local);
        }
        return points[points.length - 1][1];
    }
}
