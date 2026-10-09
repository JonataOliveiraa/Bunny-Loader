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

    // Um ícone de chefe a mais (a 2ª fase, por exemplo), como no tModLoader:
    // devolve a vaga, para o BossHeadSlot. Com npcType, vira o ícone padrão do
    // tipo. -1 sem o arquivo.
    AddBossHeadTexture(texture, npcType = -1) {
        const file = bl.path.join(this.path, ModFiles.Texture(texture, this));
        const slot = bl.npcs.addBossHead(file, npcType);
        if (slot < 0) Safe.Once('bosshead:' + file, `AddBossHeadTexture: '${texture}' não está no mod`);
        return slot;
    }

    GetContent(base) {
        return Templates.All(base, this);
    }

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
