// Anima um número de um objeto ao longo de ticks, com uma curva de Easing.
// Anda junto com os efeitos (para com o jogo pausado); num efeito que morre,
// para também.
//   ScreenUtils.Tween.To(fx, 'Intensity', 0, 30, Easing.OutQuad);
//   fx.Tween('Radius', 300, 20, Easing.OutBack, { delay: 10, onDone: () => fx.Kill() });
class ScreenTween {
    static #active = [];

    Target;
    Property;
    From = 0;
    To;
    Duration;
    Easing;
    Delay;
    Age = 0;
    OnDone;

    #started = false;
    #killed = false;

    constructor(target, property, to, duration, easing, options) {
        if (!(property in target)) throw new TypeError('Tween: ' + property + ' is not a property of the target');

        this.Target = target;
        this.Property = property;
        this.To = Number(to);
        this.Duration = Math.max(Number(duration) || 0, 0);
        this.Easing = easing ?? Easing.Linear;
        this.Delay = Math.max(Number(options.delay) || 0, 0);
        this.OnDone = options.onDone ?? null;
    }

    static To(target, property, to, duration, easing = Easing.Linear, options = {}) {
        const tween = new ScreenTween(target, property, to, duration, easing, options);
        // Um Tween novo na mesma propriedade substitui o anterior.
        for (const t of ScreenTween.#active) {
            if (t.Target === target && t.Property === property) t.Kill();
        }
        ScreenTween.#active.push(tween);
        ScreenEffects.Install();
        return tween;
    }

    static get Count() {
        return ScreenTween.#active.length;
    }

    get Done() {
        return this.#killed;
    }

    Kill() {
        this.#killed = true;
    }

    // ScreenEffects, a cada quadro, com os ticks que passaram.
    static Update(delta) {
        if (ScreenTween.#active.length === 0) return;

        for (const t of ScreenTween.#active) {
            if (!t.#killed) Safe.Run('Tween ' + t.Property, () => t.#Step(delta));
        }
        ScreenTween.#active = ScreenTween.#active.filter((t) => !t.#killed);
    }

    #Step(delta) {
        if (this.Target instanceof ModScreenEffect && !this.Target.Active) {
            this.Kill();
            return;
        }

        this.Age += delta;
        const time = this.Age - this.Delay;
        if (time < 0) return;
        if (!this.#started) {
            this.#started = true;
            this.From = Number(this.Target[this.Property]) || 0;
        }

        const t = this.Duration > 0 ? Math.min(time / this.Duration, 1) : 1;
        this.Target[this.Property] = this.From + (this.To - this.From) * this.Easing(t);
        if (t >= 1) {
            this.Kill();
            if (this.OnDone) this.OnDone(this.Target);
        }
    }
}
