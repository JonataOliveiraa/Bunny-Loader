// Os modelos de um tipo de Global, na ordem de carga. Sem filtro nativo por
// tipo: um hook de Global entra no JS para toda entidade que passa por ele.
class GlobalRegistry {
    static #NONE = Object.freeze([]);
    #methods = new WeakMap();

    constructor(Base, entityClass, field, getter) {
        this.Base = Base;
        this.entityClass = entityClass;
        this.field = field;
        this.getter = getter;
        this.list = [];
        this.cached = false;   // alguma entidade precisa de lista própria (filtro ou cópia)?
        this.ready = false;
    }

    Register(cls, kind) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof this.Base)) {
            throw new TypeError(kind + '.register(Classe): passe a classe, que estende ' + kind);
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.__perEntity = !!inst.InstancePerEntity;
        inst.__conditional = Hooks.Overrides(cls, this.Base, 'AppliesToEntity');

        this.list.push(inst);
        if (inst.__perEntity || inst.__conditional) this.cached = true;
        if (!this.ready) this.#DefineAccessors();

        Ready.Add(() => inst.AddRecipeGroups(), 'groups');
        Ready.Add(() => {
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
            inst.AddRecipes();
            inst.PostSetupContent();
        });
        return inst;
    }

    Find(entity, which) {
        for (const g of this.Of(entity)) {
            if (typeof which === 'function' ? g instanceof which : g.constructor.name === which) return g;
        }
        return undefined;
    }

    // Sem filtro e sem cópia por entidade, são os próprios modelos.
    Of(entity) {
        if (!this.list.length || !(entity.type > 0)) return GlobalRegistry.#NONE;
        if (!this.cached) return this.list;

        const current = entity[this.field];
        if (current && current.type === entity.type) {
            if (current.count !== this.list.length) this.#Append(entity, true, current.list, current.count);
            current.count = this.list.length;
            return current.list;
        }

        return this.Attach(entity, true);
    }

    // A lista da entidade: filtrada pelo AppliesToEntity, com cópia para quem é
    // por entidade. O SetDefaults chama de novo: tipo novo, estado novo.
    Attach(entity, late) {
        const list = [];
        this.#Append(entity, late, list, 0);
        entity[this.field] = { type: entity.type, list, count: this.list.length };
        return list;
    }

    #Append(entity, late, list, start) {
        for (let i = start; i < this.list.length; i++) {
            const g = this.list[i];
            if (g.__conditional && !this.Invoke(g, 'AppliesToEntity', entity, late)) continue;
            list.push(g.__perEntity ? (this.Invoke(g, 'NewInstance', entity) || g) : g);
        }
    }

    Each(entity, method, fn) {
        for (const g of this.For(entity, method)) {
            try { fn(g); }
            catch (error) { Safe.Report(g.constructor.name + '.' + method, error); }
        }
    }

    Templates(method) { return this.#ForList(this.list, method); }

    For(entity, method) {
        const templates = this.Templates(method);
        if (!templates.length) return GlobalRegistry.#NONE;
        if (!this.cached) return entity.type > 0 ? templates : GlobalRegistry.#NONE;
        return this.#ForList(this.Of(entity), method);
    }

    #ForList(list, method) {
        let entry = this.#methods.get(list);
        if (!entry || entry.length !== list.length) {
            entry = { length: list.length, methods: new Map() };
            this.#methods.set(list, entry);
        }
        let result = entry.methods.get(method);
        if (!result) {
            result = list.filter((g) => Hooks.Overrides(g.constructor, this.Base, method));
            entry.methods.set(method, result);
        }
        return result;
    }

    Invoke(g, method, entity, a, b, c, d) {
        try {
            switch (this.Base.prototype[method].length) {
                case 2: return g[method](entity, a);
                case 3: return g[method](entity, a, b);
                case 4: return g[method](entity, a, b, c);
                case 5: return g[method](entity, a, b, c, d);
                default: return g[method](entity);
            }
        }
        catch (error) { Safe.Report(g.constructor.name + '.' + method, error); }
    }

    Call(entity, method, a, b, c, d) {
        for (const g of this.For(entity, method)) this.Invoke(g, method, entity, a, b, c, d);
    }

    AllCall(entity, method, a, b, c, d) {
        let allowed = true;
        for (const g of this.For(entity, method)) {
            if (this.Invoke(g, method, entity, a, b, c, d) === false) allowed = false;
        }
        return allowed;
    }

    First(entity, method, a, b, boolean = false) {
        for (const g of this.For(entity, method)) {
            const result = this.Invoke(g, method, entity, a, b);
            if (boolean ? typeof result === 'boolean' : result != null) return result;
        }
        return undefined;
    }

    // Nenhum devolveu false?
    All(entity, method, fn) {
        let ok = true;
        this.Each(entity, method, (g) => {
            if (fn(g) === false) ok = false;
        });
        return ok;
    }

    AnyWith(entity, methods) {
        for (const g of this.Of(entity)) {
            for (const method of methods) {
                if (Hooks.Overrides(g.constructor, this.Base, method)) return true;
            }
        }
        return false;
    }

    // item.GetGlobalItem(Classe | 'Nome') lança se não se aplica; TryGetGlobalItem põe no Ref.
    #DefineAccessors() {
        this.ready = true;

        const registry = this;
        const entityClass = this.entityClass();
        const nameOf = (which) => (typeof which === 'function' ? which.name : String(which));
        Entities.Define(entityClass, this.field);

        bl.defineMethod(entityClass, this.getter, function (which) {
            const g = registry.Find(this, which);
            if (!g) throw new Error(registry.getter + ": '" + nameOf(which) + "' nao se aplica a esta entidade (tipo " + this.type + ')');

            return g;
        });

        bl.defineMethod(entityClass, 'Try' + this.getter, function (which, result) {
            const g = registry.Find(this, which);
            if (result && typeof result === 'object') result.value = g;
            return g !== undefined;
        });
    }
}
