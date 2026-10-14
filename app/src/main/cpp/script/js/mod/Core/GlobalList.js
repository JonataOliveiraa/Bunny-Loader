// Os Global que valem para um TIPO de conteúdo inteiro (todo tile, todo buff,
// toda barra de chefe, toda receita), sem cópia por entidade: um modelo por
// classe, na ordem de carga. Os de entidade (item, NPC, projétil) são o GlobalRegistry.
class GlobalList {
    #byMethod = new Map();

    // setup: o que envolve o SetStaticDefaults (o GlobalTile destrava o TileObjectData).
    constructor(Base, kind, setup = (run) => run()) {
        this.Base = Base;
        this.kind = kind;
        this.list = [];
        this.setup = setup;
    }

    Register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof this.Base)) {
            throw new TypeError(this.kind + '.register(Class): pass the class, which extends ' + this.kind);
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        this.list.push(inst);
        this.#byMethod.clear();

        Ready.Add(() => {
            Safe.Run(name + '.SetStaticDefaults', () => this.setup(() => inst.SetStaticDefaults()));
            Safe.Run(name + '.PostSetupContent', () => inst.PostSetupContent());
        });
        return inst;
    }

    Overrides(cls, method) {
        return Hooks.Overrides(cls, this.Base, method);
    }

    // Os modelos que sobrescrevem o método.
    With(method) {
        let found = this.#byMethod.get(method);
        if (!found) {
            found = this.list.filter((g) => Hooks.Overrides(g.constructor, this.Base, method));
            this.#byMethod.set(method, found);
        }
        return found;
    }

    Each(method, fn) {
        for (const g of this.With(method)) {
            try { fn(g); }
            catch (error) { Safe.Report(g.constructor.name + '.' + method, error); }
        }
    }

    // Nenhum devolveu false?
    All(method, fn) {
        let ok = true;
        this.Each(method, (g) => {
            if (fn(g) === false) ok = false;
        });
        return ok;
    }

    // Algum devolveu true?
    Any(method, fn) {
        let any = false;
        this.Each(method, (g) => {
            if (fn(g) === true) any = true;
        });
        return any;
    }

    // O primeiro resultado que não é null/undefined.
    First(method, fn) {
        for (const g of this.With(method)) {
            let result;
            try { result = fn(g); }
            catch (error) { Safe.Report(g.constructor.name + '.' + method, error); }
            if (result !== null && result !== undefined) return result;
        }
        return undefined;
    }
}
