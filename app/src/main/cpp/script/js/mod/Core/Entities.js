// Cada Item, Projectile e NPC de mod tem a própria instância num campo ao
// lado do objeto do jogo. A instância guarda o ENDEREÇO da entidade: nada do
// mod segura vivo o que o jogo descartou.
class Entities {
    static #fields = new Set();
    static #baselines = new WeakMap();   // molde -> os valores logo depois do construtor

    static Define(cls, field) {
        if (Entities.#fields.has(field)) return;

        Entities.#fields.add(field);
        bl.defineField(cls, field);
    }

    static Bind(inst, entity, field) {
        inst.__entity = bl.addressOf(entity);
        entity[field] = inst;
        return inst;
    }

    static InstanceOf(entity, field, byType) {
        if (!entity) return undefined;

        const type = entity.type;
        const template = byType.get(type);
        if (!template) return undefined;

        const current = entity[field];
        if (current && current.Type === type) return current;

        return Entities.Bind(template.NewInstance(entity), entity, field);
    }

    // No registro, logo depois do `new`: o que o molde tinha antes do registro
    // e do SetStaticDefaults (o NewInstance só leva ao novo o que mudou).
    static Baseline(template) {
        const values = new Map();
        for (const key of Reflect.ownKeys(template)) values.set(key, template[key]);
        Entities.#baselines.set(template, values);
    }

    // O NewInstance do tModLoader: uma instância nova pelo construtor, para
    // cada entidade. Os campos da classe rodam de novo (`bloodMoonMode =
    // Main.bloodMoon` vale no spawn; arrays e objetos são da entidade), e os
    // `#privados` existem. Do molde vem o que o registro e os Set*Defaults
    // estáticos puseram nele depois do construtor (Type, Mod, Texture, uma
    // textura guardada em this...). Sem a linha de base, uma cópia (Clone).
    static NewInstance(template) {
        const baseline = Entities.#baselines.get(template);
        if (!baseline) return Entities.Clone(template);

        const inst = new template.constructor();
        for (const key of Reflect.ownKeys(template)) {
            if (baseline.has(key) && Object.is(baseline.get(key), template[key])) continue;
            Object.defineProperty(inst, key, Object.getOwnPropertyDescriptor(template, key));
        }
        inst.__entity = 0;
        return inst;
    }

    // O MemberwiseClone do C#. Pelos descritores: um campo com o nome de um
    // getter da base não tem setter, e o Object.assign lançava.
    static Clone(inst) {
        const copy = Object.create(Object.getPrototypeOf(inst), Object.getOwnPropertyDescriptors(inst));
        copy.__entity = 0;
        return copy;
    }

    static Of(inst) {
        return inst.__entity ? bl.objectAt(inst.__entity) : undefined;
    }
}

Entities.Define(Terraria.Item, 'ModItem');
Entities.Define(Terraria.Projectile, 'ModProjectile');
Entities.Define(Terraria.NPC, 'ModNPC');
