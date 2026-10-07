// O NPC.HitModifiers do tModLoader: o golpe num NPC antes de acontecer. Os
// ModifyHit* mexem nele; o loader faz a conta (a mesma do tModLoader: dano de
// origem, bônus, defesa, crítico, final) e entrega o resultado ao StrikeNPC
// do jogo. AddableFloat e MultipliableFloat do C# são números aqui:
// `modifiers.ArmorPenetration += 5`, `modifiers.TargetDamageMultiplier *= 2`.
class HitModifiers {
    #base;
    #limit = Infinity;
    #crit = null;
    #noKnockback = false;
    #instantKill = false;
    #hideText = false;

    constructor(baseDamage = 0, baseKnockback = 0, hitDirection = 0, crit = false, defense = 0, damageType = null) {
        this.#base = { damage: baseDamage, knockback: baseKnockback, crit: !!crit };
        this.DamageType = damageType;
        this.HitDirection = hitDirection;
        this.SuperArmor = false;
        this.SourceDamage = new StatModifier();
        this.FlatBonusDamage = 0;
        this.ScalingBonusDamage = 0;
        this.TargetDamageMultiplier = 1;
        this.Defense = new StatModifier(1, 1, 0, defense);
        this.ArmorPenetration = 0;
        this.ScalingArmorPenetration = 0;
        this.DefenseEffectiveness = 0.5;
        this.CritDamage = new StatModifier(2, 1);
        this.NonCritDamage = new StatModifier();
        this.FinalDamage = new StatModifier();
        this.Knockback = new StatModifier();
        this.HitDirectionOverride = null;
    }

    SetMaxDamage(limit) { this.#limit = Math.min(this.#limit, Math.max(limit, 1)); }
    DisableCrit() { this.#crit = false; }
    SetCrit() { if (this.#crit === null) this.#crit = true; }
    DisableKnockback() { this.#noKnockback = true; }
    SetInstantKill() { this.#instantKill = true; }
    HideCombatText() { this.#hideText = true; }

    // Os nomes de antes (o golpe em números), ainda aceitos.
    get damage() { return this.#base.damage; }
    set damage(value) { this.#base.damage = Number(value) || 0; }
    get knockBack() { return this.#base.knockback; }
    set knockBack(value) { this.#base.knockback = Number(value) || 0; }
    get hitDirection() { return this.HitDirectionOverride ?? this.HitDirection; }
    set hitDirection(value) { this.HitDirectionOverride = value; }
    get crit() { return this.#crit ?? this.#base.crit; }
    set crit(value) { this.#crit = !!value; }

    GetDamage(baseDamage, crit) {
        crit = this.#crit ?? crit;
        if (this.SuperArmor) {
            const damage = crit ? this.CritDamage.Additive * this.CritDamage.Multiplicative : 1;
            return Math.max(1, Math.min(Math.trunc(damage), Math.min(this.#limit, 4)));
        }
        let damage = this.SourceDamage.ApplyTo(baseDamage);
        damage += this.FlatBonusDamage + this.ScalingBonusDamage * damage;
        damage *= this.TargetDamageMultiplier;
        let defense = Math.max(this.Defense.ApplyTo(0), 0);
        const penetration = defense * Math.min(Math.max(this.ScalingArmorPenetration, 0), 1) + this.ArmorPenetration;
        defense = Math.max(defense - penetration, 0);
        damage = Math.max(damage - defense * this.DefenseEffectiveness, 1);
        damage = (crit ? this.CritDamage : this.NonCritDamage).ApplyTo(damage);
        return Math.max(1, Math.min(Math.trunc(this.FinalDamage.ApplyTo(damage)), this.#limit));
    }

    GetKnockback(baseKnockback) {
        return this.#noKnockback ? 0 : Math.max(this.Knockback.ApplyTo(baseKnockback), 0);
    }

    ToHitInfo(baseDamage = this.#base.damage, crit = this.#base.crit, baseKnockback = this.#base.knockback) {
        const finite = (value) => (Number.isFinite(value) ? value : 0);
        return new HitInfo({
            DamageType: this.DamageType,
            SourceDamage: Math.max(Math.trunc(finite(this.SourceDamage.ApplyTo(baseDamage))), 1),
            Damage: this.#instantKill ? 1 : finite(this.GetDamage(baseDamage, crit)),
            Crit: this.#crit ?? !!crit,
            Knockback: finite(this.GetKnockback(baseKnockback)),
            HitDirection: this.HitDirectionOverride ?? this.HitDirection,
            InstantKill: this.#instantKill,
            HideCombatText: this.#hideText,
        });
    }
}
