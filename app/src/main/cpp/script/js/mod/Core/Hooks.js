class Hooks {
    static #installed = new Set();

    static Once(key, install) {
        if (Hooks.#installed.has(key)) return;

        Hooks.#installed.add(key);
        install();
    }

    static Overrides(cls, base, name) {
        return cls.prototype[name] !== base.prototype[name];
    }
}
