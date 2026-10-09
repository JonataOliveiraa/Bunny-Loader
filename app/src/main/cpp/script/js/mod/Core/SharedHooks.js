// Um hook só por método do jogo, para os loaders que só rodam algo antes ou
// depois dele. Cada hook a mais no mesmo método pagava de novo o despacho e a
// entrada no JS, a cada chamada.
//
// A ordem é a de uma cadeia de hooks, em que o instalado primeiro fica por
// fora: os de antes na ordem de registro, os de depois na ordem inversa.
// Um erro num deles vai para o log e não impede os outros nem o método.
class SharedHooks {
    static #hooks = new Map();   // chave -> { filter, before, after }

    static Before(key, method, label, fn, filter) {
        SharedHooks.#Get(key, method, filter).before.push({ label, fn });
    }

    static After(key, method, label, fn, filter) {
        SharedHooks.#Get(key, method, filter).after.push({ label, fn });
    }

    static #Get(key, method, filter) {
        const shape = JSON.stringify(filter || {});
        const known = SharedHooks.#hooks.get(key);
        if (known) {
            if (known.filter !== shape) throw new Error(`SharedHooks '${key}': filtro diferente do primeiro (${shape} != ${known.filter})`);
            return known;
        }

        const hook = { filter: shape, before: [], after: [] };
        SharedHooks.#hooks.set(key, hook);

        const before = hook.before, after = hook.after;
        const callback = (original, ...args) => {
            for (let k = 0; k < before.length; k++) SharedHooks.#Call(before[k], args);
            const result = original(...args);
            for (let k = after.length - 1; k >= 0; k--) SharedHooks.#Call(after[k], args);
            return result;
        };
        if (filter) method.hook(callback, filter);
        else method.hook(callback);
        return hook;
    }

    static #Call(entry, args) {
        try {
            entry.fn(...args);
        } catch (e) {
            Safe.Report(entry.label, e);
        }
    }
}
