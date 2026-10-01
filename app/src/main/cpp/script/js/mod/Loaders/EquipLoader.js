// As texturas vestidas dos itens de mod (armadura, asas, acessórios), como o
// EquipLoader do tModLoader. Cada uma ganha um slot depois dos do jogo: no
// celular o ArmorIDs.<tipo>.Count é um estático que o código lê (não uma
// constante), então sobe junto com as tabelas indexadas pelo slot, e o jogo
// desenha o slot novo como qualquer outro.
class EquipLoader {
    // ids: a classe do ArmorIDs; slot: o campo do Item; textures: as do
    // TextureAssets (todas apontam para a mesma, como no ExMod do TL: o jogo
    // desenha a primeira, mas outras telas podem ler as outras);
    // itemTypes: a tabela slot -> item (manequim); newFraming: a folha
    // composta do corpo e das mãos.
    static KINDS = {
        Head: { ids: 'Head', slot: 'headSlot', textures: ['ArmorHead'], itemTypes: 'headType' },
        Body: {
            ids: 'Body', slot: 'bodySlot', itemTypes: 'bodyType', newFraming: true,
            textures: ['ArmorBodyComposite', 'ArmorBody', 'FemaleBody', 'ArmorArm'],
        },
        Legs: { ids: 'Legs', slot: 'legSlot', textures: ['ArmorLeg'], itemTypes: 'legType' },
        HandsOn: { ids: 'HandOn', slot: 'handOnSlot', textures: ['AccHandsOnComposite', 'AccHandsOn'], newFraming: true },
        HandsOff: { ids: 'HandOff', slot: 'handOffSlot', textures: ['AccHandsOffComposite', 'AccHandsOff'], newFraming: true },
        Back: { ids: 'Back', slot: 'backSlot', textures: ['AccBack'] },
        Front: { ids: 'Front', slot: 'frontSlot', textures: ['AccFront'] },
        Shoes: { ids: 'Shoe', slot: 'shoeSlot', textures: ['AccShoes'] },
        Waist: { ids: 'Waist', slot: 'waistSlot', textures: ['AccWaist'] },
        Wings: { ids: 'Wing', slot: 'wingSlot', textures: ['Wings'] },
        Shield: { ids: 'Shield', slot: 'shieldSlot', textures: ['AccShield'] },
        Neck: { ids: 'Neck', slot: 'neckSlot', textures: ['AccNeck'] },
        Face: { ids: 'Face', slot: 'faceSlot', textures: ['AccFace'] },
        Beard: { ids: 'Beard', slot: 'beardSlot', textures: ['AccBeard'] },
        Balloon: { ids: 'Balloon', slot: 'balloonSlot', textures: ['AccBalloon'] },
    };

    // Os acessórios guardam o slot num sbyte do Item.
    static MAX_SBYTE_SLOTS = 128;

    static #pending = [];            // EquipTexture ainda sem slot
    static #textures = new Map();    // 'Head:293' -> EquipTexture
    static #byName = new Map();      // 'Head:ExampleHelmet' -> slot
    static #slots = new Map();       // tipo do item -> { campo do Item: slot }
    static #vanilla = new Map();     // tipo de equipamento -> o Count do jogo
    static #installed = false;

    // No registro do item (bl.mod ainda é o dele): as texturas <Texture>_<tipo>,
    // ou só as de `static AutoloadEquip = [EquipType.Head, ...]` (o atributo do
    // tModLoader; [] desliga, para quem registra à mão com AddEquipTexture).
    static Autoload(inst, cls, name) {
        const only = Array.isArray(cls.AutoloadEquip) ? cls.AutoloadEquip : null;
        for (const kind of Object.keys(EquipLoader.KINDS)) {
            if (only && !only.includes(kind)) continue;

            const file = EquipLoader.#FileOf(inst.Texture + '_' + kind);
            if (file) EquipLoader.#Add(file, kind, inst, name, null);
        }
    }

    // AddEquipTexture([mod,] textura, tipo, item = null, nome = null, equipTexture = null),
    // como no tModLoader, no Load() do item (antes da instalação). O slot só
    // existe depois: GetEquipSlot(nome, tipo) do SetStaticDefaults em diante.
    static AddEquipTexture(...args) {
        if (EquipLoader.#IsMod(args[0])) args.shift();
        const [texture, kind, item = null, name = null, equipTexture = null] = args;
        if (!EquipLoader.KINDS[kind]) throw new TypeError('EquipLoader.AddEquipTexture: tipo "' + kind + '" nao existe (EquipType)');
        if (!item && !name) throw new TypeError('EquipLoader.AddEquipTexture: passe o item ou um nome');
        if (EquipLoader.#installed) throw new Error('EquipLoader.AddEquipTexture: tarde demais, as texturas ja foram instaladas');

        const file = EquipLoader.#FileOf(texture);
        if (!file) throw new Error("EquipLoader.AddEquipTexture: nao achei a textura '" + texture + "'");

        EquipLoader.#Add(file, kind, item, name || item.constructor.name, equipTexture);
        return -1;
    }

    static GetEquipTexture(kind, slot) { return EquipLoader.#textures.get(kind + ':' + slot); }

    // GetEquipSlot([mod,] nome, tipo): -1 se não há.
    static GetEquipSlot(...args) {
        if (EquipLoader.#IsMod(args[0])) args.shift();
        const slot = EquipLoader.#byName.get(args[1] + ':' + args[0]);
        return slot === undefined ? -1 : slot;
    }

    static VanillaCount(kind) { return EquipLoader.#vanilla.get(kind) || 0; }

    // Os tipos de equipamento que ganharam textura de mod.
    static InstalledKinds() { return EquipLoader.#kinds; }
    static #kinds = [];

    // No SetDefaults de cada Item, antes do do mod (que pode trocar).
    static Apply(item, type) {
        const slots = EquipLoader.#slots.get(type);
        if (!slots) return;

        for (const field in slots) item[field] = slots[field];
    }

    // Uma vez, na primeira vez que um item de mod precisa do slot (a amostra do
    // jogo ou o SetStaticDefaults): nessa hora o jogo já carregou as texturas
    // dele. Antes disso, subir o Count faria o jogo procurar Armor_Head_293.
    static Install() {
        if (EquipLoader.#installed) return;
        EquipLoader.#installed = true;
        if (!EquipLoader.#pending.length) return;

        const byKind = new Map();
        for (const tex of EquipLoader.#pending) {
            if (!byKind.has(tex.Type)) byKind.set(tex.Type, []);
            byKind.get(tex.Type).push(tex);
        }
        for (const [kind, list] of byKind) {
            Safe.Run('texturas vestidas (' + kind + ')', () => EquipLoader.#InstallKind(kind, list));
        }
        EquipLoader.#pending = [];
    }

    // O this.Mod do tModLoader: a classe Mod do mod ou o bl.mod (o que os moldes guardam).
    static #IsMod(x) {
        return x instanceof Mod || (!!x && typeof x === 'object' && 'uuid' in x && 'path' in x);
    }

    // O caminho completo (na instalação, quem chama não é mais o mod).
    static #FileOf(texture) {
        const root = bl.mod && bl.mod.path;
        if (!root) return null;

        const file = bl.path.join(root, ModFiles.Texture(texture, root));
        return bl.file.exists(file) ? file : null;
    }

    static #Add(file, kind, item, name, equipTexture) {
        const tex = equipTexture || new EquipTexture();
        tex.Texture = file;
        tex.Name = name;
        tex.Type = kind;
        tex.Item = item;
        EquipLoader.#pending.push(tex);
        if (tex.constructor !== EquipTexture) EquipLoader.#WantHooks(tex.constructor);

        // Os HidesTopSkin... do tModLoader são marcados no SetStaticDefaults: conferidos com tudo pronto.
        if (kind === EquipType.Body || kind === EquipType.Legs) {
            Hooks.Once('equip.DrawFlags', () => Ready.Add(() => ArmorSetLoader.CheckDrawFlags()));
        }
    }

    // Uma EquipTexture própria (a cabeça do ExampleCostume) liga os ganchos que muda.
    static #WantHooks(cls) {
        const has = (name) => Hooks.Overrides(cls, EquipTexture, name);
        if (has('IsVanitySet') || has('PreUpdateVanitySet') || has('UpdateVanitySet') || has('FrameEffects')) ArmorSetLoader.WantFrame();
        if (has('IsVanitySet') || has('ArmorSetShadows')) ArmorSetLoader.WantShadows();
        if (has('SetMatch')) ArmorSetLoader.WantSetMatch();
        if (has('VerticalWingSpeeds')) WingLoader.Want('Vertical');
        if (has('HorizontalWingSpeeds')) WingLoader.Want('Horizontal');
        if (has('WingUpdate')) WingLoader.Want('Update');
    }

    static #InstallKind(kind, list) {
        const k = EquipLoader.KINDS[kind];
        const Ids = Terraria.ID.ArmorIDs[k.ids];
        const vanilla = Ids.Count;
        if (!(vanilla > 0)) throw new Error('ArmorIDs.' + k.ids + '.Count ainda nao existe');

        const byte = !k.itemTypes;
        const total = vanilla + list.length;
        if (byte && total > EquipLoader.MAX_SBYTE_SLOTS) {
            throw new Error(list.length + ' textura(s) de ' + kind + ' passam do limite de ' +
                            EquipLoader.MAX_SBYTE_SLOTS + ' do jogo');
        }

        bl.items.growEquipSets(k.ids, vanilla, total);
        Ids.Count = total;
        EquipLoader.#vanilla.set(kind, vanilla);
        EquipLoader.#kinds.push(kind);
        if (kind === EquipType.Wings) EquipLoader.#PrepareWingStats(vanilla, total);

        const T = Terraria.GameContent.TextureAssets;
        for (const field of k.textures) {
            if (T[field].length < total) T[field] = T[field].cloneResized(total);
        }
        if (k.itemTypes && Terraria.Item[k.itemTypes].length < total) {
            Terraria.Item[k.itemTypes] = Terraria.Item[k.itemTypes].cloneResized(total);
        }

        list.forEach((tex, i) => {
            const slot = tex.Slot = vanilla + i;
            const asset = ContentLookup.RequestFile(tex.Texture);
            for (const field of k.textures) T[field][slot] = asset;

            if (k.newFraming) Ids.Sets.UsesNewFramingCode[slot] = true;
            if (kind === EquipType.Wings) Ids.Sets.Stats[slot] = Terraria.DataStructures.WingStats.Default;
            EquipLoader.#textures.set(kind + ':' + slot, tex);
            EquipLoader.#byName.set(kind + ':' + tex.Name, slot);

            // O item veste a primeira textura de cada tipo registrada para ele.
            const item = tex.Item;
            if (!item) return;

            if (!EquipLoader.#slots.has(item.Type)) EquipLoader.#slots.set(item.Type, {});
            const slots = EquipLoader.#slots.get(item.Type);
            if (slots[k.slot] === undefined) slots[k.slot] = slot;
            if (k.itemTypes) Terraria.Item[k.itemTypes][slot] = item.Type;
        });

        bl.log('texturas vestidas: ' + list.length + ' de ' + kind + ' (slots ' + vanilla + '..' + (total - 1) + ')');
    }

    // O ArmorIDs.Wing.Sets.Stats nasce no WingStatsInitializer.Load, que o
    // jogo chama no Initialize_AlmostEverything DEPOIS do SetStaticDefaults
    // dos itens (onde o mod escreve as estatísticas da asa dele): a tabela é
    // criada aqui antes, e a que o jogo refizer recebe de volta os slots de mod.
    static #PrepareWingStats(first, total) {
        const init = Terraria.Initializers.WingStatsInitializer;
        Hooks.Once('equip.WingStats', () => init.Load.hook((original) => {
            const old = Terraria.ID.ArmorIDs.Wing.Sets.Stats;
            original();
            const now = Terraria.ID.ArmorIDs.Wing.Sets.Stats;
            if (!old || !now || old === now) return;

            for (let slot = first; slot < total && slot < old.length && slot < now.length; slot++) now[slot] = old[slot];
        }));

        if (!Terraria.ID.ArmorIDs.Wing.Sets.Stats) init.Load();
    }
}
