// Os modelos de um tipo de Global, na ordem de carga. Sem filtro nativo por
// tipo: um hook de Global entra no JS para toda entidade que passa por ele.
class GlobalRegistry {
    static #NONE = Object.freeze([]);

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
        if (current && current.type === entity.type) return current.list;

        return this.Attach(entity, true);
    }

    // A lista da entidade: filtrada pelo AppliesToEntity, com cópia para quem é
    // por entidade. O SetDefaults chama de novo: tipo novo, estado novo.
    Attach(entity, late) {
        const list = [];
        for (const g of this.list) {
            const n = g.constructor.name;
            if (g.__conditional && !Safe.Run(n + '.AppliesToEntity', () => g.AppliesToEntity(entity, late))) continue;

            list.push(g.__perEntity ? (Safe.Run(n + '.NewInstance', () => g.NewInstance(entity)) || g) : g);
        }

        entity[this.field] = { type: entity.type, list };
        return list;
    }

    Each(entity, method, fn) {
        for (const g of this.Of(entity)) {
            if (Hooks.Overrides(g.constructor, this.Base, method)) Safe.Run(g.constructor.name + '.' + method, () => fn(g));
        }
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
