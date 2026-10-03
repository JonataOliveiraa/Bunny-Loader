// O modelo de cada classe registrada (ModContent.GetInstance) e o Mod dela.
class Templates {
    static #byClass = new Map();

    static Adopt(cls, inst) {
        if (Templates.#byClass.has(cls)) throw new Error(cls.name + ' ja foi registrado');

        inst.Mod = bl.mod;
        Templates.#byClass.set(cls, inst);
    }

    static Get(cls) {
        return Templates.#byClass.get(cls);
    }

    static All(base, mod = null) {
        if (typeof base !== 'function') throw new TypeError('GetContent(Classe): passe a classe base, como ModItem');
        const found = [];
        for (const inst of Templates.#byClass.values()) {
            if (inst instanceof base && (!mod || inst.Mod === mod)) found.push(inst);
        }
        return found;
    }

    static HideFromMenu(kind, inst, type, fromGame) {
        const hide = inst.HideFromModMenu || Safe.Run(inst.constructor.name + ' (Mod Menu)', fromGame);
        if (hide) bl.menu.hide(kind, type);
    }
}
