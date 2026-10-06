class GenPass {
    constructor(name, weight = 1) {
        if (typeof name !== 'string' || !name) throw new TypeError('GenPass: nome vazio');
        if (!Number.isFinite(weight) || weight < 0) throw new RangeError('GenPass: peso deve ser finito e não negativo');
        const pass = Terraria.GameContent.Generation.PassLegacy.new();
        pass['void .ctor(string name, WorldGenLegacyMethod method, double weight)'](name, null, weight);
        this.Native = pass;
        SystemWorldHooks.Track(this);
    }

    get Name() { return this.Native.Name; }
    set Name(value) { this.Native.Name = value; }
    get Weight() { return this.Native.Weight; }
    set Weight(value) {
        if (!Number.isFinite(value) || value < 0) throw new RangeError('GenPass.Weight: peso inválido');
        this.Native.Weight = value;
    }
    get Enabled() { return this.Native.Enabled; }
    Disable() { this.Native['void Disable()'](); }
    Enable() { this.Native['void Enable()'](); }
    Apply(progress, configuration) {
        if (this.Enabled) this.ApplyPass(progress, configuration);
    }
    ApplyPass(progress, configuration) { throw new Error('GenPass: implemente ApplyPass'); }
}

class PassLegacy extends GenPass {
    constructor(name, method, weight = 1) {
        if (typeof method !== 'function') throw new TypeError('PassLegacy: passe uma função');
        super(name, weight);
        this.Method = method;
    }
    ApplyPass(progress, configuration) { this.Method(progress, configuration); }
}
