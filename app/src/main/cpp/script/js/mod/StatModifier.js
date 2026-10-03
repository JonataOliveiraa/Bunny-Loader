// O StatModifier do tModLoader: (valor + Base) * Additive * Multiplicative + Flat.
// No C# é um valor imutável e o `+=` troca o inteiro; aqui é um objeto e os
// campos mudam direto: `player.GetDamage(c).Additive += 0.1` é o `+= 0.1f`
// do C#, e `.Multiplicative *= 1.12` o `*= 1.12f`.
class StatModifier {
    constructor(additive = 1, multiplicative = 1, flat = 0, base = 0) {
        this.Additive = additive;
        this.Multiplicative = multiplicative;
        this.Flat = flat;
        this.Base = base;
    }

    static get Default() { return new StatModifier(); }

    ApplyTo(baseValue) {
        return (baseValue + this.Base) * this.Additive * this.Multiplicative + this.Flat;
    }

    CombineWith(m) {
        return new StatModifier(this.Additive + m.Additive - 1, this.Multiplicative * m.Multiplicative,
                                this.Flat + m.Flat, this.Base + m.Base);
    }

    Scale(scale) {
        return new StatModifier(1 + (this.Additive - 1) * scale, 1 + (this.Multiplicative - 1) * scale,
                                this.Flat * scale, this.Base * scale);
    }

    Undo(currentValue) {
        return (currentValue - this.Flat) / (this.Multiplicative * this.Additive) - this.Base;
    }

    Clone() {
        return new StatModifier(this.Additive, this.Multiplicative, this.Flat, this.Base);
    }

    Equals(m) {
        return !!m && this.Additive === m.Additive && this.Multiplicative === m.Multiplicative &&
               this.Flat === m.Flat && this.Base === m.Base;
    }

    // O `damage` dos ModifyWeaponDamage: vale como número (`return damage * 2`,
    // o jeito de antes) e como StatModifier (`damage.Additive += 0.1`, o do
    // tModLoader). O que o mod devolver em número ganha; senão vale o modificador.
    static ForValue(value) {
        const m = new StatModifier();
        Object.defineProperty(m, 'valueOf', { value: () => value });
        return m;
    }

    static Resolve(result, modifier, value) {
        if (typeof result === 'number') return result;
        return modifier.ApplyTo(value);
    }
}
