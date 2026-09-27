class Mod {
    constructor() {
        const target = ModRegistry.TakeAdopting();
        if (!target) throw new TypeError("Mod: a classe e criada pelo carregador (export default no arquivo de entrada), sem new");

        Object.setPrototypeOf(target, new.target.prototype);
        return target;
    }

    get id() { return this[MOD_INFO].id; }
    get uuid() { return this[MOD_INFO].uuid; }
    get name() { return this[MOD_INFO].name; }
    get version() { return this[MOD_INFO].version; }
    get path() { return this[MOD_INFO].path; }
    get root() { return this[MOD_INFO].root; }
    get dataDirectory() { return ModRegistry.DataDirectory(this.uuid); }

    toString() { return 'Mod(' + (this.id || this.uuid) + ')'; }

    Load() {}
    AddRecipeGroups() {}
    AddRecipes() {}
    PostSetupContent() {}

    GetPacket() {
        ModNet.Install();
        return new ModPacket(this);
    }

    HandlePacket(reader, whoAmI) {}

    // Mod que ainda não carregou (ou quebrou) não responde "nada" calado.
    Call(...args) {
        ModRegistry.Sync();
        const info = this[MOD_INFO];
        const id = info.id || info.uuid;

        if (info.state === 'pending') throw new Error("Mod '" + id + "' ainda nao carregou: chame o Call a partir do PostSetupContent");
        if (info.state === 'failed') throw new Error("Mod '" + id + "' falhou ao carregar");

        return undefined;
    }

    static register() {
        throw new TypeError("Mod.register saiu: use 'export default class <Nome> extends Mod' no arquivo de entrada");
    }
}
