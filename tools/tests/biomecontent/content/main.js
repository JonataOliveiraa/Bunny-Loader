// O resto do ModBiome como no tModLoader (docs/local/PLANO-MODBIOME.md):
//   - nome: DisplayName e TownNPCDialogueName (do arquivo, da classe ou o
//     padrão), e o texto que a fala do morador lê (TownNPCMoodBiomes.<NameKey>);
//   - felicidade: SetBiomeAffection com a classe do bioma; o preço do Guia cai e
//     a fala cita o bioma quando o jogador está nele;
//   - Bestiário: o bioma do SpawnModBiomes na entrada do NPC (o nome, o ícone
//     30 x 30 e o fundo 115 x 65 do mod) e o filtro; e os do Example Mod;
//   - spawn: EditSpawnFlags antes de tudo (o campo mudado chega ao sorteio) e o
//     SpawnCondition (as condições do jogo somam 1 no ponto do spawn).
// Loga "biomecontent <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { NPCID } = Terraria.ID;
const B = Terraria.GameContent.Bestiary;
const text = (key) => Terraria.Localization.Language['string GetTextValue(string key)'](key);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('biomecontent ' + label + ': ok');
        else { fails++; bl.log('biomecontent ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('biomecontent ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class TestBiome extends ModBiome {
    static on = false;
    IsBiomeActive(player) { return TestBiome.on; }
    constructor() { super(); this.Music = -1; }
}

export class NamedBiome extends ModBiome {
    DisplayName = { 'en-US': 'Named', 'pt-BR': 'Nomeado' };
    constructor() { super(); this.Music = -1; }
}

export class BiomeBlob extends ModNPC {
    SetDefaults() {
        this.NPC.width = 24;
        this.NPC.height = 18;
        this.NPC.aiStyle = 0;
        this.NPC.lifeMax = 50;
        this.NPC.noGravity = true;
        this.SpawnModBiomes = [TestBiome];
    }
}

const state = { force: false, flagsEdit: false };
const seen = { flags: [], pool: [] };

export class BiomeSpawnGlobal extends GlobalNPC {
    EditSpawnFlags(info) {
        if (!state.flagsEdit) return;
        seen.flags.push({ x: info.SpawnTileX, player: info.Player.whoAmI });
        info.noGroundWorms = true;
    }
    EditSpawnRate(player, rate, max) {
        if (state.force) { rate.value = 1; max.value = 200; }
    }
    EditSpawnPool(pool, info) {
        if (!state.flagsEdit) return;
        const roots = ['NebulaTower', 'VortexTower', 'StardustTower', 'SolarTower', 'Sky', 'Invasion', 'Bartender', 'SpiderCave',
            'DesertCave', 'HardmodeJungleWater', 'HardmodeCrimsonWater', 'Ocean', 'BeachAngler', 'JungleWater', 'CavePiranha',
            'CaveJellyfish', 'WaterCritter', 'BoundCaveNPC', 'TownCritter', 'Dungeon', 'Meteor', 'OldOnesArmy', 'FrostMoon',
            'PumpkinMoon', 'SolarEclipse', 'HardmodeMushroomWater', 'OverworldMushroom', 'UndergroundMushroom', 'CorruptWorm',
            'UndergroundMimic', 'OverworldMimic', 'Wraith', 'HoppinJack', 'DoctorBones', 'LacBeetle', 'WormCritter', 'MouseCritter',
            'SnailCritter', 'FrogCritter', 'HardmodeJungle', 'JungleTemple', 'UndergroundJungle', 'SurfaceJungle', 'SandstormEvent',
            'Mummy', 'DarkMummy', 'LightMummy', 'OverworldHallow', 'EnchantedSword', 'Crimson', 'Corruption', 'Overworld',
            'Underground', 'Underworld', 'Cavern'];
        let sum = 0;
        for (const name of roots) sum += SpawnCondition[name].Chance;
        seen.pool.push({
            noGroundWorms: info.noGroundWorms, sum, y: info.SpawnTileY,
            overworld: SpawnCondition.Overworld.Active && SpawnCondition.Overworld.Chance,
            slime: SpawnCondition.OverworldDaySlime.Chance,
        });
        pool.Clear();   // nada nasce: o teste só olha
    }
}

function names() {
    const biome = ModContent.GetInstance(TestBiome);
    check('DisplayName padrão: o nome da classe separado', () =>
        (biome.DisplayName.Key === 'Mods.test-biomecontent.Biomes.TestBiome.DisplayName' && biome.DisplayName.Value === 'Test Biome') ||
        biome.DisplayName.Key + ' = ' + biome.DisplayName.Value);
    check('TownNPCDialogueName padrão, e a chave da fala do morador', () =>
        (biome.TownNPCDialogueName.Value === 'the Test Biome' && text('TownNPCMoodBiomes.' + biome.ShoppingNameKey) === 'the Test Biome') ||
        biome.TownNPCDialogueName.Value + ' / ' + text('TownNPCMoodBiomes.' + biome.ShoppingNameKey));
    check('DisplayName da classe, por cultura', () => {
        const want = ModLocalization.ActiveCultureName === 'pt-BR' ? 'Nomeado' : 'Named';
        const got = ModContent.GetInstance(NamedBiome).DisplayName.Value;
        return got === want || got + ' (cultura ' + ModLocalization.ActiveCultureName + ')';
    });
    check('Example Mod: os nomes do Localization do mod', () => {
        const d = text('Mods.examplemod.Biomes.ExampleSurfaceBiome.DisplayName');
        const t = text('Mods.examplemod.Biomes.ExampleSurfaceBiome.TownNPCDialogueName');
        return (['Example Surface', 'Superfície de Exemplo'].includes(d) && /Example|Exemplo/.test(t)) || d + ' / ' + t;
    });
}

function elementsOf(type) {
    const entry = Main.BestiaryDB['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
    const out = [];
    if (!entry) return out;
    for (let i = 0; i < entry.Info.Count; i++) {
        const e = entry.Info.get_Item(i);
        if (e.GetType().Name === 'SpawnConditionBestiaryInfoElement') out.push(e);
    }
    return out;
}

function bestiary() {
    const key = 'Mods.test-biomecontent.Biomes.TestBiome.DisplayName';
    const element = elementsOf(ModContent.NPCType('BiomeBlob')).find((e) => e.GetDisplayNameKey() === key);
    check('Bestiário: o bioma do SpawnModBiomes na entrada do NPC', () => !!element || 'sem o elemento ' + key);
    check('Bestiário: o ícone 30 x 30 do mod no filtro', () => {
        const tex = new Ref(null), frame = new Ref(Rectangle.new(0, 0, 0, 0));
        element.GetDisplay(tex, frame);
        return (tex.value && tex.value.Width === 30 && tex.value.Height === 30 && frame.value.Width === 30) ||
            `${tex.value && tex.value.Width}x${tex.value && tex.value.Height}, quadro ${frame.value.Width}`;
    });
    check('Bestiário: o fundo 115 x 65 do mod no retrato', () => {
        const bg = element['Asset`1 GetBackgroundImage()']();
        return (bg && bg.Value.Width === 115 && bg.Value.Height === 65) || 'fundo ' + (bg ? bg.Value.Width + 'x' + bg.Value.Height : 'nenhum');
    });
    check('Bestiário: o filtro do bioma', () => {
        const filters = Main.BestiaryDB.Filters;
        for (let i = 0; i < filters.Count; i++) {
            const f = filters.get_Item(i);
            if (f.GetType().Name === 'ByInfoElement' && f.GetDisplayNameKey() === key) return true;
        }
        return 'nenhum filtro com ' + key + ' em ' + filters.Count;
    });
    check('Example Mod: o ExampleSlimeNPC com o bioma de exemplo', () => {
        const slime = ModContent.NPCType('examplemod/ExampleSlimeNPC');
        const keys = elementsOf(slime).map((e) => e.GetDisplayNameKey());
        return keys.includes('Mods.examplemod.Biomes.ExampleSurfaceBiome.DisplayName') || keys.join(', ');
    });
    check('Example Mod: o ExamplePerson com o bioma de exemplo', () => {
        const person = ModContent.NPCType('examplemod/ExamplePerson');
        const keys = elementsOf(person).map((e) => e.GetDisplayNameKey());
        return keys[0] === 'Mods.examplemod.Biomes.ExampleSurfaceBiome.DisplayName' || keys.join(', ');
    });
    // O SetStaticDefaults do morador roda no registro: o bioma precisa vir antes.
    check('Example Mod: o ExamplePerson ama o bioma de exemplo', () => {
        const person = ModContent.NPCType('examplemod/ExamplePerson');
        const traits = Main.ShopHelper._database['PersonalityProfile GetByNPCID(int npcId)'](person).ShopModifiers;
        const biomes = [];
        for (let i = 0; i < traits.Count; i++) {
            const t = traits.get_Item(i);
            if (t.GetType().Name !== 'BiomePreferenceListTrait') continue;
            const pref = t._preferences.get_Item(0);
            biomes.push(pref.Biome.NameKey + '=' + pref.Affection);
        }
        return (traits.Count === 10 && biomes.includes('Mods.examplemod.Biomes.ExampleSurfaceBiome=' + AffectionLevel.Love)) ||
            traits.Count + ' gostos: ' + biomes.join(', ');
    });
}

// ---- felicidade ----
let guide = null, createdGuide = false, before = null, home = null;
const shopping = () => Main.ShopHelper['ShoppingSettings GetShoppingSettings(Player player, NPC npc)'](Main.player[Main.myPlayer], guide);
function happinessStart() {
    new NPCHappiness(NPCID.Guide).SetBiomeAffection(TestBiome, AffectionLevel.Love);
    for (let i = 0; i < 200; i++) if (Main.npc[i].active && Main.npc[i].type === NPCID.Guide) guide = Main.npc[i];
    if (!guide) {
        const p = Main.player[Main.myPlayer];
        const N = Terraria.NPC;
        const idx = N.NewNPC(N.GetSpawnSourceForNaturalSpawn(), Math.floor(p.Center.X) + 40, Math.floor(p.Bottom.Y), NPCID.Guide, 0, 0, 0, 0, 0, 255);
        guide = Main.npc[idx];
        createdGuide = true;
    }
    // Sem casa (ou longe dela) o jogo põe o preço em 1000 e depois o prende em
    // 1,5: o desconto do bioma não apareceria. A casa fica onde ele está, e
    // volta no fim.
    home = { homeless: guide.homeless, x: guide.homeTileX, y: guide.homeTileY };
    guide.homeless = false;
    guide.homeTileX = Math.floor(guide.Center.X / 16);
    guide.homeTileY = Math.floor(guide.Bottom.Y / 16);
    TestBiome.on = false;
    const s = shopping();
    before = { price: s.PriceAdjustment, report: s.HappinessReport };
    TestBiome.on = true;
}
function happinessCheck() {
    const s = shopping();
    check('felicidade: no bioma amado, o Guia cobra menos', () =>
        s.PriceAdjustment < before.price - 0.01 || `antes ${before.price}, no bioma ${s.PriceAdjustment}`);
    check('felicidade: a fala cita o bioma (TownNPCDialogueName)', () =>
        String(s.HappinessReport).includes('the Test Biome') || JSON.stringify(String(s.HappinessReport).slice(0, 300)));
    TestBiome.on = false;
    guide.homeless = home.homeless;
    guide.homeTileX = home.x;
    guide.homeTileY = home.y;
    if (createdGuide) guide.active = false;
}

function spawn() {
    state.force = true;
    state.flagsEdit = true;
    const spawnNPC = Terraria.NPC['void SpawnNPC()'];
    for (let k = 0; k < 3000 && !seen.pool.length; k++) spawnNPC();
    state.force = false;
    state.flagsEdit = false;
    check('EditSpawnFlags: antes do ponto (-1), com o jogador-alvo', () => {
        const f = seen.flags[0];
        return (f && f.x === -1 && f.player === Main.myPlayer) || JSON.stringify(seen.flags.slice(0, 2));
    });
    check('EditSpawnFlags: o campo mudado chega ao sorteio', () => {
        const p = seen.pool[0];
        return (p && p.noGroundWorms === true) || JSON.stringify(p);
    });
    check('SpawnCondition: as condições do jogo somam 1 no ponto', () => {
        const p = seen.pool[0];
        return (p && Math.abs(p.sum - 1) < 1e-4) || JSON.stringify(p);
    });
    check('SpawnCondition: de dia na superfície, Overworld e OverworldDaySlime', () => {
        const p = seen.pool[0];
        if (!p) return 'sem sorteio';
        if (p.y > Main.worldSurface) return true;   // o ponto caiu embaixo: não se aplica
        return (p.overworld > 0 && p.slime > 0) || JSON.stringify(p);
    });
}

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    self.statLife = self.statLifeMax2;
    self.immune = true;
    self.immuneTime = 30;
    if (frame === 1) { Main.dayTime = true; Main.time = 27000; }
    if (frame === 10) { check('nomes', names); check('bestiário', bestiary); check('felicidade (começo)', happinessStart); }
    if (frame === 20) check('felicidade', happinessCheck);
    if (frame === 30) check('spawn', spawn);
    if (frame === 40) {
        done = true;
        bl.log('biomecontent FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('biomecontent: carregado');

export default class TestBiomeContent extends Mod {}
