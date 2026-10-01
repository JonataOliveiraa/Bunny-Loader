// Uma nuvem de mod, como o ModCloud do tModLoader: a textura da classe
// (Content/Clouds/X.js -> Content/Clouds/X.png). SpawnChance é o peso
// no sorteio: as comuns contra as 22 do jogo (peso 1 cada); as raras
// (RareCloud) contra as raras do jogo. Um PNG numa pasta Clouds/ sem
// classe vira nuvem comum sozinho; CloudLoader.AddCloudFromTexture (no
// Mod.Load) escolhe peso e raridade.
class ModCloud {
    Type = undefined;
    Texture = this.constructor.name;
    get RareCloud() { return false; }

    SetStaticDefaults() {}
    SpawnChance() { return 1; }
    // Quando uma nuvem deste tipo aparece (o jogo já a posicionou).
    OnSpawn(cloud) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModCloud)) {
            throw new TypeError('ModCloud.register(Classe): passe a classe, que estende ModCloud');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        CloudLoader.Add(inst);
        return inst.Type;
    }
}

// A nuvem só de textura (o SimpleModCloud do tModLoader).
class SimpleModCloud extends ModCloud {
    constructor(name, texture, spawnChance, rare) {
        super();
        this.Name = name;
        this.Texture = texture;
        this.spawnChance = spawnChance;
        this.rare = rare;
    }
    get RareCloud() { return this.rare; }
    SpawnChance() { return this.spawnChance; }
}
