// O Player.HurtModifiers do tModLoader: o golpe no jogador antes de
// acontecer (ModifyHurt, ModifyHitByNPC, ModifyHitPlayer...). SourceDamage e
// IncomingDamageMultiplier valem antes da defesa; ArmorPenetration tira
// defesa; FinalDamage e SetMaxDamage, depois dela. O jogo do celular não tem
// repulsão variável no jogador: Knockback fica para quem lê.
class HurtModifiers {
    #base;
    #limit = Infinity;
    #cancelled = false;
    #dust = false;
    #sound = false;

    constructor(source = null, damage = 0, hitDirection = 0, pvp = false, cooldownCounter = -1, dodgeable = true, quiet = false, crit = false) {
        this.#base = { damage, quiet: !!quiet, crit: !!crit };
        this.DamageSource = source;
        this.PvP = !!pvp;
        this.CooldownCounter = cooldownCounter;
        this.Dodgeable = !!dodgeable;
        this.HitDirection = hitDirection;
        this.SourceDamage = new StatModifier();
        this.IncomingDamageMultiplier = 1;
        this.FinalDamage = new StatModifier();
        this.ArmorPenetration = 0;
        this.ScalingArmorPenetration = 0;
        this.Knockback = new StatModifier();
        this.KnockbackImmunityEffectiveness = 1;
        this.HitDirectionOverride = null;
    }

    SetMaxDamage(limit) { this.#limit = Math.min(this.#limit, Math.max(limit, 1)); }
    Cancel() { this.#cancelled = true; }
    DisableDust() { this.#dust = true; }
    DisableSound() { this.#sound = true; }

    get Cancelled() { return this.#cancelled; }
    get DustDisabled() { return this.#dust; }
    get SoundDisabled() { return this.#sound; }
    get MaxDamage() { return this.#limit; }

    // Os nomes de antes (o golpe em números), ainda aceitos.
    get damage() { return this.#base.damage; }
    set damage(value) { this.#base.damage = Number(value) || 0; }
    get hitDirection() { return this.HitDirectionOverride ?? this.HitDirection; }
    set hitDirection(value) { this.HitDirectionOverride = value; }
    get quiet() { return this.#base.quiet || (this.#dust && this.#sound); }
    set quiet(value) { this.#base.quiet = !!value; }
    get crit() { return this.#base.crit; }
    set crit(value) { this.#base.crit = !!value; }
    get dodgeable() { return this.Dodgeable; }
    set dodgeable(value) { this.Dodgeable = !!value; }

    // O dano que entra no Hurt do jogo (antes da defesa).
    SourceValue() {
        const value = this.SourceDamage.ApplyTo(this.#base.damage) * this.IncomingDamageMultiplier;
        return Number.isFinite(value) ? Math.max(0, Math.trunc(value)) : 0;
    }

    // A defesa que sobra depois da penetração.
    EffectiveDefense(defense) {
        const penetration = defense * Math.min(Math.max(this.ScalingArmorPenetration, 0), 1) + this.ArmorPenetration;
        return Math.max(defense - penetration, 0);
    }

    // Depois da defesa: o final e o teto.
    Final(afterDefense) {
        const value = this.FinalDamage.ApplyTo(afterDefense);
        return Math.max(1, Math.min(Number.isFinite(value) ? value : 1, this.#limit));
    }

    // O que o jogador já levou, para os OnHit/OnHurt.
    ToHurtInfo(damageDone) {
        return new HurtInfo({
            DamageSource: this.DamageSource, PvP: this.PvP, CooldownCounter: this.CooldownCounter, Dodgeable: this.Dodgeable,
            HitDirection: this.hitDirection, SourceDamage: this.SourceValue(), Damage: damageDone,
            Knockback: Math.max(this.Knockback.ApplyTo(0), 0), Cancelled: this.#cancelled,
            DustDisabled: this.#dust, SoundDisabled: this.#sound, Quiet: this.quiet, Crit: this.crit,
        });
    }
}
