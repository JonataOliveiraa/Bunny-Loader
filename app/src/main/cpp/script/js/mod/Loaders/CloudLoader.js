// As nuvens de mod, como o CloudLoader do tModLoader. Tipo depois dos do jogo
// (CloudID.Count), textura em TextureAssets.Cloud (a única tabela do tamanho
// das nuvens) com o jogo pronto. O sorteio é o do tModLoader: no
// Cloud.addCloud, uma comum de mod contra as 22 do jogo; no RollRareCloud,
// uma rara de mod contra as 18 raras do jogo (15 no mundo de aniversário).
// O Draw do tModLoader ainda não existe aqui.
class CloudLoader {
    static ByType = new Map();
    static #pending = [];
    static #installed = false;
    static #rolledRare = false;   // o addCloud em curso já sorteou uma rara

    static Add(inst) {
        inst.Type = Terraria.ID.CloudID.Count + CloudLoader.ByType.size;
        CloudLoader.ByType.set(inst.Type, inst);

        const file = ModFiles.Texture(inst.Texture);
        inst.__file = bl.file.exists(file) ? bl.mod.path + '/' + file : null;
        CloudLoader.#pending.push(inst);
        if (CloudLoader.#pending.length === 1) Ready.Add(() => CloudLoader.#Install(), 'setup');
        Hooks.Once('cloud', () => CloudLoader.#Hook());
    }

    // Uma nuvem só de textura (Assets/Textures/<textura>.png), no Mod.Load.
    static AddCloudFromTexture(texture, spawnChance = 1, rareCloud = false) {
        if (!bl.mod) throw new Error('CloudLoader.AddCloudFromTexture: só na carga do mod (Mod.Load)');
        const path = ModFiles.TextureName(texture);
        const name = path.slice(path.lastIndexOf('/') + 1);
        const inst = new SimpleModCloud(name, path, spawnChance, rareCloud);
        inst.Mod = bl.mod;
        CloudLoader.Add(inst);
        return inst;
    }

    // Todo PNG em Assets/Textures/Clouds sem nuvem com o mesmo nome (classe ou
    // AddCloudFromTexture) vira nuvem comum de peso 1, como no tModLoader.
    static Autoload() {
        const mod = bl.mod;
        if (!mod) return;
        const mine = [...CloudLoader.ByType.values()].filter((c) => c.Mod === mod);
        for (const file of ModFiles.ListTextures('Clouds')) {
            if (!file.endsWith('.png')) continue;
            const name = file.slice(file.lastIndexOf('/') + 1, -4);
            if (mine.some((c) => (c.Name || c.constructor.name) === name)) continue;
            CloudLoader.AddCloudFromTexture('Clouds/' + name);
        }
    }

    static #Install() {
        const pending = CloudLoader.#pending.splice(0);
        if (!pending.length) return;

        const T = Terraria.GameContent.TextureAssets;
        const total = Terraria.ID.CloudID.Count + CloudLoader.ByType.size;
        if (T.Cloud.length < total) T.Cloud = T.Cloud.cloneResized(total);
        for (const inst of pending) {
            const name = inst.Name || inst.constructor.name;
            Safe.Run(name + ' (textura)', () => {
                if (!inst.__file) throw new Error('sem a textura ' + inst.Texture);
                T.Cloud[inst.Type] = bl.loadTextureAsset(inst.__file);
            });
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }
        CloudLoader.#installed = true;
        bl.log('nuvens de mod: ' + pending.length + ' (tipos ' + pending[0].Type + '..' + (total - 1) + ')');
    }

    // O ChooseCloud do tModLoader: 0 é "uma do jogo".
    static ChooseCloud(vanillaPool, rare) {
        if (!CloudLoader.#installed) return 0;
        const pool = [[0, vanillaPool]];
        for (const [type, inst] of CloudLoader.ByType) {
            if (!!inst.RareCloud !== rare) continue;
            const weight = Safe.Run((inst.Name || inst.constructor.name) + '.SpawnChance', () => inst.SpawnChance());
            if (weight > 0) pool.push([type, weight]);
        }
        if (pool.length === 1) return 0;

        let total = 0;
        for (const entry of pool) total += Math.max(0, entry[1]);
        let choice = Math.random() * total;
        for (const [type, weight] of pool) {
            if (choice < weight) return type;
            choice -= weight;
        }
        return 0;
    }

    static #Hook() {
        const Cloud = Terraria.Cloud;
        const Main = Terraria.Main;

        Cloud['int RollRareCloud()'].hook((original) => {
            CloudLoader.#rolledRare = true;
            const type = CloudLoader.ChooseCloud(Main.tenthAnniversaryWorld ? 15 : 18, true);
            return type || original();
        });

        // A nuvem nova fica na primeira vaga livre de Main.cloud.
        Cloud['void addCloud()'].hook((original) => {
            const clouds = Main.cloud;
            let slot = -1;
            for (let i = 0; i < 200; i++) {
                if (!clouds[i].active) { slot = i; break; }
            }
            CloudLoader.#rolledRare = false;
            original();
            if (slot < 0) return;

            const cloud = clouds[slot];
            if (!cloud.active) return;
            if (!CloudLoader.#rolledRare) {
                const type = CloudLoader.ChooseCloud(22, false);
                if (type) {
                    cloud.type = type;
                    const texture = Terraria.GameContent.TextureAssets.Cloud[type].Value;
                    cloud.width = Math.trunc(texture.Width * cloud.scale);
                    cloud.height = Math.trunc(texture.Height * cloud.scale);
                }
            }
            const inst = CloudLoader.ByType.get(cloud.type);
            if (inst) Safe.Run((inst.Name || inst.constructor.name) + '.OnSpawn', () => inst.OnSpawn(cloud));
        });
    }
}
