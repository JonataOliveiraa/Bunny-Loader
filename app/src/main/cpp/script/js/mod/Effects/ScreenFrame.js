// A lista de um quadro: os passos de todos os efeitos, os alvos que eles
// pediram e se alguém lê o quadro anterior. O ScreenEffects monta uma por
// quadro e a manda ao núcleo (bl.screen.submit).
class ScreenFrame {
    Draws = [];
    History = false;

    #targets = new Map();   // nome@escala -> ScreenTarget

    Target(name, scale, persistent) {
        const s = Number(scale);
        if (!(s > 0 && s <= 1)) throw new RangeError('cmd.Target: scale must be in (0, 1]');

        const key = name + '@' + s;
        let target = this.#targets.get(key);
        if (!target) {
            target = new ScreenTarget(this.#targets.size + 1, String(name), s, persistent);
            this.#targets.set(key, target);
        }
        return target;
    }

    Submit() {
        const targets = [];
        for (const t of this.#targets.values()) {
            targets.push({ id: t.Texture, name: t.Name, scale: t.Scale, persistent: t.Persistent });
        }
        bl.screen.submit(this.Draws, targets, { history: this.History });
    }
}
