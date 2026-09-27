class Safe {
    static #reported = new Set();

    // Um erro do mod num hook de todo quadro vira uma linha de log só.
    static Run(label, fn) {
        try {
            return fn();
        } catch (e) {
            Safe.Once(label, 'erro em ' + label + ': ' + e + (e && e.stack ? '\n' + e.stack : ''));
            return undefined;
        }
    }

    static Once(key, text) {
        if (Safe.#reported.has(key)) return;

        Safe.#reported.add(key);
        bl.log(text);
    }
}
