// O NPC.HitInfo do tModLoader: o golpe que aconteceu (os OnHit*). Damage é o
// dano que o NPC levou; SourceDamage, o de origem, antes da defesa.
class HitInfo {
    constructor(values = {}) {
        this.DamageType = values.DamageType ?? null;
        this.SourceDamage = Math.max(values.SourceDamage | 0, 1);
        this.Damage = Math.max(values.Damage | 0, 1);
        this.Crit = !!values.Crit;
        this.HitDirection = values.HitDirection | 0;
        this.Knockback = Number(values.Knockback) || 0;
        this.InstantKill = !!values.InstantKill;
        this.HideCombatText = !!values.HideCombatText;
    }
}
