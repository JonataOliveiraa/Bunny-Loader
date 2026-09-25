// Buffs de mod: tabelas, barra (o GUIBuffs.Draw zerava tipo >= 389),
// Update/Apply/ReApply no jogador e no NPC, a defesa do Example Mod e o save
// pelo nome. Precisa do Example Mod ligado; rode DUAS vezes: a segunda
// confere o buff que a primeira salvou. Loga "buffs <caso>: ok | FALHOU".
const Main = Terraria.Main;
const count = {};
const bump = (k) => { count[k] = (count[k] || 0) + 1; };

class TestBuff extends ModBuff {
    DisplayName = 'Buff de Teste';
    Description = 'Vale {0}';
    ModifyDescription() { this.Description = this.Description.replace('{0}', 7); }
    UpdatePlayer(player, idx) { bump('UpdatePlayer'); }
    ApplyPlayer(player, time) { bump('ApplyPlayer'); }
    ReApplyPlayer(player, time, idx) { bump('ReApplyPlayer'); return false; }
}

class TestNpcBuff extends ModBuff {
    DisplayName = 'Buff de NPC';
    Texture = 'TestBuff';
    SetStaticDefaults() { Main.debuff[this.Type] = true; }
    UpdateNPC(npc, idx) { bump('UpdateNPC'); }
    ApplyNPC(npc, time) { bump('ApplyNPC'); }
}

class TestSaveBuff extends ModBuff {
    DisplayName = 'Buff Salvo';
    Texture = 'TestBuff';
}

const TEST = ModBuff.register(TestBuff);
const NPCBUFF = ModBuff.register(TestNpcBuff);
const SAVED = ModBuff.register(TestSaveBuff);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('buffs ' + label + ': ok');
        else { fails++; bl.log('buffs ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('buffs ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];
const buffName = (t) => Terraria.Lang['string GetBuffName(int id)'](t);
const buffDesc = (t) => Terraria.Lang['string GetBuffDescription(int id)'](t);
const addBuff = (p, t, time) => p['void AddBuff(int type, int time, bool fromNetPvP)'](t, time, false);
const findBuff = (e, t) => e['int FindBuffIndex(int type)'](t);
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

// O buff de defesa do Example Mod: de outro mod, pelo nome no jogo.
function modBuffByName(...names) {
    for (let t = bl.buffs.vanillaCount; t < bl.buffs.vanillaCount + 64; t++) {
        if (!bl.buffs.isModBuff(t)) break;
        if (names.includes(buffName(t))) return t;
    }
    return -1;
}

let DEF = -1, baseDefense = 0, npc = null;

function atEntry() {
    const i = findBuff(me(), SAVED);
    if (i < 0) bl.log('buffs save: nenhum buff salvo (primeira rodada?)');
    else check('save: buff reposto pelo nome', () => {
        const t = me().buffTime[i];
        return (t > 1000) || 'tempo ' + t;
    });
}

function start() {
    const p = me();
    DEF = modBuffByName('Defesa', 'Defense');
    check('tipos e tabelas', () => {
        if (TEST < bl.buffs.vanillaCount || NPCBUFF !== TEST + 1) return `tipos ${TEST}, ${NPCBUFF}`;
        const n = SAVED + 1;
        if (Main.debuff.length < n || Main.buffNoSave.length < n) return 'Main.debuff ' + Main.debuff.length;
        if (Terraria.ID.BuffID.Sets.IsWellFed.length < n) return 'BuffID.Sets ' + Terraria.ID.BuffID.Sets.IsWellFed.length;
        if (!Terraria.GameContent.TextureAssets.Buff[TEST]) return 'sem textura';
        return Main.debuff[NPCBUFF] === true || 'SetStaticDefaults nao rodou';
    });
    check('nome e descricao', () => {
        const n = buffName(TEST), d = buffDesc(TEST);
        return (n === 'Buff de Teste' && d === 'Vale 7') || `"${n}" / "${d}"`;
    });
    check('buffImmune do jogador e do NPC', () => {
        const n = SAVED + 1;
        const pl = p.buffImmune.length, nl = Main.npc[0].buffImmune.length;
        return (pl >= n && nl >= n) || `jogador ${pl}, NPC ${nl}`;
    });
    check('Example Mod: buff da pocao', () => {
        if (DEF < 0) return 'ExampleDefenseBuff nao achado';
        const potion = Terraria.Item.new();
        potion['void .ctor()']();
        for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 400 && bl.items.isModItem(t); t++) {
            if (['Poção de Buff', 'Buff Potion'].includes(Terraria.Lang['string GetItemNameValue(int id)'](t))) {
                potion['void SetDefaults(int Type, ItemVariant variant)'](t, null);
                break;
            }
        }
        const colors = Terraria.ID.ItemID.Sets.DrinkParticleColors[potion.type];
        if (!colors || colors.length !== 3) return 'DrinkParticleColors ' + (colors && colors.length);
        return (potion.buffType === DEF && potion.buffTime === 5400) || `buffType ${potion.buffType}`;
    });

    baseDefense = p.statDefense;
    addBuff(p, TEST, 90);
    if (DEF >= 0) addBuff(p, DEF, 600);
    const c = p.Center;
    npc = Main.npc[newNpc(null, Math.floor(c.X + 240), Math.floor(c.Y), 1, 0, 0, 0, 0, 0, 255)];
    npc['void AddBuff(int type, int time, bool quiet)'](NPCBUFF, 120, false);
}

function reapply() {
    addBuff(me(), TEST, 5000);
}

function midChecks() {
    const p = me();
    check('barra: o buff de mod sobrevive ao GUIBuffs.Draw', () => findBuff(p, TEST) >= 0 || 'sumiu');
    check('UpdatePlayer, ApplyPlayer', () =>
        ((count.UpdatePlayer || 0) >= 20 && count.ApplyPlayer === 1) || JSON.stringify(count));
    check('ReApplyPlayer false: o tempo nao renova', () => {
        const i = findBuff(p, TEST);
        return (count.ReApplyPlayer === 1 && i >= 0 && p.buffTime[i] <= 90) ||
            `ReApply ${count.ReApplyPlayer}, tempo ${i >= 0 ? p.buffTime[i] : '-'}`;
    });
    check('Example Mod: defesa +10', () =>
        p.statDefense === baseDefense + 10 || `antes ${baseDefense}, agora ${p.statDefense}`);
    check('NPC: ApplyNPC, UpdateNPC', () =>
        (count.ApplyNPC === 1 && (count.UpdateNPC || 0) >= 20 && findBuff(npc, NPCBUFF) >= 0) || JSON.stringify(count));
}

function lateChecks() {
    const p = me();
    check('o tempo vence', () => findBuff(p, TEST) < 0 || 'ainda ativo');
    if (DEF >= 0) {
        const i = findBuff(p, DEF);
        if (i >= 0) p['void DelBuff(int b)'](i);
    }
    npc.active = false;
    addBuff(p, SAVED, 100000);
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](
        Main.ActivePlayerFileData, false, true);
    bl.log('buffs: buff de save posto e personagem salvo');
    bl.log('buffs FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 1) { Main.dayTime = true; Main.time = 27000; atEntry(); }
    if (frames <= 260 && !self.dead) self.statLife = self.statLifeMax2;
    if (frames === 60) start();
    if (frames === 62) reapply();
    if (frames === 100) midChecks();
    if (frames === 200) lateChecks();
});
bl.log('buffs: carregado');
