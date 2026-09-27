// Cada Item, Projectile e NPC de mod tem a própria instância (cópia do molde)
// num campo ao lado do objeto do jogo. A instância guarda o ENDEREÇO da
// entidade: nada do mod segura vivo o que o jogo descartou.
class Entities {
    static #fields = new Set();

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

        return Entities.Bind(template.Clone(entity), entity, field);
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
