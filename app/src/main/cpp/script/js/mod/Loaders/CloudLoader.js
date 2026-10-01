// As nuvens de mod, como o CloudLoader do tModLoader. Tipo depois dos do jogo
// (CloudID.Count). Duas tabelas por tipo crescem com o jogo pronto: a textura
// (TextureAssets.Cloud) e a máscara do horizonte do celular
// (TextureMaskManager.CloudMasks, ver #GrowMasks). O sorteio é o do tModLoader: no
// Cloud.addCloud, uma comum de mod contra as 22 do jogo; no RollRareCloud,
// uma rara de mod contra as 18 raras do jogo (15 no mundo de aniversário).
// O Draw do tModLoader ainda não existe aqui.
class CloudLoader {
    static ByType = new Map();
    static #pending = [];
    static #installed = false;
    static #rolledRare = false;   // o addCloud em curso já sorteou uma rara
    static #rareType = 0;         // e ela é de mod (o tipo)

    static Add(inst) {
        inst.Type = Terraria.ID.CloudID.Count + CloudLoader.ByType.size;
        CloudLoader.ByType.set(inst.Type, inst);

        const file = ModFiles.Texture(inst.Texture);
        inst.__file = bl.file.exists(file) ? bl.mod.path + '/' + file : null;
        CloudLoader.#pending.push(inst);
        if (CloudLoader.#pending.length === 1) Ready.Add(() => CloudLoader.#Install(), 'setup');
        Hooks.Once('cloud', () => CloudLoader.#Hook());
    }

    // Uma nuvem só de textura (o caminho no mod, sem extensão), no Mod.Load.
    static AddCloudFromTexture(texture, spawnChance = 1, rareCloud = false) {
        if (!bl.mod) throw new Error('CloudLoader.AddCloudFromTexture: só na carga do mod (Mod.Load)');
        const path = ModFiles.TextureName(texture);
        const name = path.slice(path.lastIndexOf('/') + 1);
        const inst = new SimpleModCloud(name, path, spawnChance, rareCloud);
        inst.Mod = bl.mod;
        CloudLoader.Add(inst);
        return inst;
    }

    // Todo PNG numa pasta Clouds/ do mod sem nuvem com o mesmo nome (classe ou
    // AddCloudFromTexture) vira nuvem comum de peso 1, como no tModLoader.
    static Autoload() {
        const mod = bl.mod;
        if (!mod) return;
        const mine = [...CloudLoader.ByType.values()].filter((c) => c.Mod === mod);
        for (const file of ModFiles.ListTextures('Clouds')) {
            if (!file.endsWith('.png')) continue;
            const name = file.slice(file.lastIndexOf('/') + 1, -4);
            if (mine.some((c) => (c.Name || c.constructor.name) === name)) continue;
            CloudLoader.AddCloudFromTexture(file.slice(0, -4));
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
                inst.__drawable = true;
            });
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }
        Safe.Run('nuvens de mod: máscaras', () => CloudLoader.#GrowMasks(total, pending));
        CloudLoader.#installed = true;
        bl.log('nuvens de mod: ' + pending.length + ' (tipos ' + pending[0].Type + '..' + (total - 1) + ')');
    }

    // O renderizador do horizonte do celular (NextHorizonRenderer.DrawCloud) lê
    // TextureMaskManager.CloudMasks[tipo] sem conferir o limite: a máscara diz
    // que pedaços da nuvem tapam o sol. Sem uma para o tipo de mod, a leitura
    // passa do fim do array, dá NullReference no meio do desenho, o lote das
    // nuvens fica aberto e todo quadro seguinte o DrawSurfaceBG para ali: só o
    // céu na tela até fechar o jogo. As máscaras vêm do Awake (LoadMasks),
    // antes desta instalação. outputWidth 0: IsSolid sempre false, a nuvem de
    // mod é desenhada e só não escurece o sol.
    static #GrowMasks(total, pending) {
        const Manager = bl.classOf('', 'TextureMaskManager');
        let masks = Manager.CloudMasks;
        if (!masks) return;
        if (masks.length < total) Manager.CloudMasks = masks = masks.cloneResized(total);

        const TextureMask = bl.classOf('', 'TextureMask');
        const T = Terraria.GameContent.TextureAssets;
        for (const inst of pending) {
            if (masks[inst.Type]) continue;
            const mask = TextureMask.new();
            mask['void .ctor()']();
            const asset = T.Cloud[inst.Type];
            const texture = asset ? asset.Value : null;
            mask.srcWidth = texture ? texture.Width : 0;
            mask.srcHeight = texture ? texture.Height : 0;
            masks[inst.Type] = mask;
        }
    }

    // O ChooseCloud do tModLoader: 0 é "uma do jogo".
    static ChooseCloud(vanillaPool, rare) {
        if (!CloudLoader.#installed) return 0;
        const pool = [[0, vanillaPool]];
        for (const [type, inst] of CloudLoader.ByType) {
            // Sem textura o desenho do jogo quebraria (TextureAssets.Cloud nulo): não sorteia.
            if (!inst.__drawable || !!inst.RareCloud !== rare) continue;
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
            CloudLoader.#rareType = type;
            return type || original();
        });

        // A nuvem nova fica na primeira vaga livre de Main.cloud. O jogo chama
        // isto a cada quadro enquanto faltam nuvens, e achar a vaga pela ponte
        // são até 200 leituras: só quando a nuvem é de mod. A comum sorteia
        // antes (a vaga ainda está livre); a rara de mod, dentro do original,
        // é achada depois pelo tipo (nasce com Alpha 0).
        Cloud['void addCloud()'].hook((original) => {
            const clouds = Main.cloud;
            const common = CloudLoader.ChooseCloud(22, false);
            let slot = -1;
            if (common) {
                for (let i = 0; i < 200; i++) {
                    if (!clouds[i].active) { slot = i; break; }
                }
            }
            CloudLoader.#rolledRare = false;
            CloudLoader.#rareType = 0;
            original();

            if (CloudLoader.#rolledRare) {
                const rare = CloudLoader.#rareType;
                slot = -1;
                if (rare) {
                    for (let i = 0; i < 200; i++) {
                        const c = clouds[i];
                        if (c.active && c.type === rare && c.Alpha === 0) { slot = i; break; }
                    }
                }
            }
            if (slot < 0) return;

            const cloud = clouds[slot];
            if (!cloud.active) return;
            if (!CloudLoader.#rolledRare) {
                cloud.type = common;
                const texture = Terraria.GameContent.TextureAssets.Cloud[common].Value;
                cloud.width = Math.trunc(texture.Width * cloud.scale);
                cloud.height = Math.trunc(texture.Height * cloud.scale);
            }
            const inst = CloudLoader.ByType.get(cloud.type);
            if (inst) Safe.Run((inst.Name || inst.constructor.name) + '.OnSpawn', () => inst.OnSpawn(cloud));
        });
    }
}
