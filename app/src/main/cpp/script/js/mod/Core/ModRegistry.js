// Cada pacote tem UM objeto Mod, criado antes de qualquer arquivo de entrada
// rodar: quem guardou a referência antes vê o Call do outro mod depois. A
// classe do `export default` do arquivo de entrada vira esse objeto.
const MOD_INFO = Symbol('modInfo');

class ModRegistry {
    static #rows = bl.__mods;
    static #callerUuid = bl.__callerMod;
    static #dataDirectory = bl.__modDataDirectory;
    static #byUuid = new Map();
    static #final = false;
    static #adopting = null;

    // Relê o registro nativo enquanto algum mod está pendente; depois, só o cache.
    static Sync() {
        if (ModRegistry.#final) return;

        let pending = false;
        for (const row of ModRegistry.#rows()) {
            const mod = ModRegistry.#byUuid.get(row.uuid);
            if (mod) Object.assign(mod[MOD_INFO], row);
            else ModRegistry.#byUuid.set(row.uuid, Object.create(Mod.prototype, { [MOD_INFO]: { value: row } }));

            if (row.state === 'pending') pending = true;
        }
        ModRegistry.#final = !pending && ModRegistry.#byUuid.size > 0;
    }

    static ByUuid(uuid) {
        ModRegistry.Sync();
        return ModRegistry.#byUuid.get(uuid);
    }

    static All() {
        ModRegistry.Sync();
        return [...ModRegistry.#byUuid.values()].filter((m) => m[MOD_INFO].state !== 'failed');
    }

    static Caller() {
        const uuid = ModRegistry.#callerUuid();
        return uuid ? ModRegistry.ByUuid(uuid) : undefined;
    }

    static DataDirectory(uuid) {
        return ModRegistry.#dataDirectory(uuid);
    }

    // Pelo id do manifesto ou pelo uuid. Dois mods com o mesmo id: nenhum.
    static Find(name, api) {
        if (typeof name !== 'string' || !name) throw new TypeError(api + ': espera o id do mod (texto)');

        ModRegistry.Sync();
        let found = null;
        let count = 0;
        for (const mod of ModRegistry.#byUuid.values()) {
            const info = mod[MOD_INFO];
            if (info.state === 'failed') continue;
            if (info.uuid === name) return mod;
            if (info.id === name) {
                found = mod;
                count++;
            }
        }
        if (count < 2) return found;

        Safe.Once('ModLoader:' + name, 'ModLoader: ' + count + " mods com o id '" + name + "'; peca pelo uuid");
        return null;
    }

    static TakeAdopting() {
        const target = ModRegistry.#adopting;
        ModRegistry.#adopting = null;
        return target;
    }

    // A classe Mod do pacote passa a ser o objeto dele. Load na hora; o resto
    // quando o conteúdo estiver pronto.
    static Adopt(mod, cls) {
        const info = mod[MOD_INFO];
        if (info.cls) throw new Error("'" + mod.id + "' ja tem a classe " + info.cls.name + ' (um Mod por pacote)');

        ModRegistry.#adopting = mod;
        try {
            new cls();
        } catch (e) {
            Object.setPrototypeOf(mod, Mod.prototype);
            throw e;
        } finally {
            ModRegistry.#adopting = null;
        }
        info.cls = cls;

        const name = cls.name;
        if (Hooks.Overrides(cls, Mod, 'HandlePacket')) ModNet.Install();

        mod.Load();
        Ready.Add(() => Safe.Run(name + '.AddRecipeGroups', () => mod.AddRecipeGroups()), 'groups');
        Ready.Add(() => {
            Safe.Run(name + '.AddRecipes', () => mod.AddRecipes());
            Safe.Run(name + '.PostSetupContent', () => mod.PostSetupContent());
        }, 'mods');
        return mod;
    }

    static ClassName(mod) {
        const info = mod[MOD_INFO];
        return info.cls ? info.cls.name : 'Mod';
    }
}

delete bl.__mods;
delete bl.__callerMod;
delete bl.__modDataDirectory;

Object.defineProperty(bl, 'mod', {
    get: () => ModRegistry.Caller(),
    configurable: true,
    enumerable: true,
});
