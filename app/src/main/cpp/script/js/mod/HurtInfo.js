// O Player.HurtInfo do tModLoader: o golpe que o jogador levou (OnHurt,
// OnHitByNPC, OnHitPlayer, OnHitPvp...). Damage é o que o Hurt do jogo tirou.
class HurtInfo {
    constructor(values = {}) {
        this.DamageSource = values.DamageSource ?? null;
        this.PvP = !!values.PvP;
        this.CooldownCounter = values.CooldownCounter ?? -1;
        this.Dodgeable = values.Dodgeable !== false;
        this.SourceDamage = Math.max(values.SourceDamage | 0, 1);
        this.Damage = Math.max(values.Damage | 0, 1);
        this.HitDirection = values.HitDirection | 0;
        this.Knockback = Number(values.Knockback) || 0;
        this.Cancelled = !!values.Cancelled;
        this.DustDisabled = !!values.DustDisabled;
        this.SoundDisabled = !!values.SoundDisabled;
        this.Quiet = !!values.Quiet;
        this.Crit = !!values.Crit;
    }
}
