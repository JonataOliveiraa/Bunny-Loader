class ModLoader {
    static TryGetMod(name, result) {
        const mod = ModRegistry.Find(name, 'ModLoader.TryGetMod');

        if (result !== undefined) {
            if (result === null || typeof result !== 'object') {
                throw new TypeError('ModLoader.TryGetMod(id, ref): o segundo argumento e um Ref (new Ref())');
            }
            result.value = mod;
        }
        return mod !== null;
    }

    static GetMod(name) {
        const mod = ModRegistry.Find(name, 'ModLoader.GetMod');
        if (!mod) throw new Error("ModLoader.GetMod: nenhum mod '" + name + "' carregado (se ele e opcional, use TryGetMod)");

        return mod;
    }

    static HasMod(name) {
        return ModRegistry.Find(name, 'ModLoader.HasMod') !== null;
    }

    static get Mods() {
        return ModRegistry.All();
    }
}

Object.freeze(ModLoader);
