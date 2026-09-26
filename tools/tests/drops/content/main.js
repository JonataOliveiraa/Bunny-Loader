// O sistema de drop do JOGO solta item de mod na morte de NPC de mod: as
// regras do ModifyNPCLoot (ExampleSlimeNPC: gel sempre, Exemplo de Item 1 em 3;
// ExampleBoss: 15 a 30 Exemplos de Item sempre). Mata 30 slimes e 1 chefe e
// conta o que caiu (o chefe, pela diferenca). Loga "drops ...".
const Main = Terraria.Main;
const GEL = Terraria.ID.ItemID.Gel;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, ' +
                            'float ai0, float ai1, float ai2, float ai3, int Target)'];
const strike = 'double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('drops ' + label + ': ok');
        else { fails++; bl.log('drops ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('drops ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}

function count(type) {
    let n = 0;
    for (let i = 0; i < Main.item.length; i++) {
        const it = Main.item[i];
        if (it.active && it.type === type) n += it.stack;
    }
    return n;
}

function describeRules(type) {
    const rules = Main.ItemDropsDB.GetRulesForNPCID(type, false);
    const out = [];
    for (let i = 0; i < rules.Count; i++) {
        const r = rules.get_Item(i);
        const cls = r.GetType().Name;
        out.push(cls + (r.itemId !== undefined ? '(' + r.itemId + ')' : ''));
    }
    return out.join(', ');
}

function spawnAndKill(type) {
    const p = Main.player[Main.myPlayer];
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const i = newNpc(source, Math.floor(p.position.X + 120), Math.floor(p.position.Y - 40), type, 0, 0, 0, 0, 0, 255);
    const npc = Main.npc[i];
    npc.playerInteraction[Main.myPlayer] = true;
    npc[strike](999999, 0, 1, false, false, false, Main.myPlayer);
    return npc;
}

let items = null, npcs = null, boss = null, exBefore = 0;
let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) {
        check('preparo', () => {
            items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
            npcs = byName(bl.npcs.vanillaCount, bl.npcs.isModNpc, ModNPC.getModNPC);
            bl.log('drops regras slime: ' + describeRules(npcs.ExampleSlimeNPC));
            bl.log('drops regras chefe: ' + describeRules(npcs.ExampleBoss));
            return (items.ExampleItem > 0 && npcs.ExampleSlimeNPC > 0 && npcs.ExampleBoss > 0) || 'tipos ' + JSON.stringify({ items: items.ExampleItem, npcs });
        });
    }
    if (frames > 60 && frames <= 90) spawnAndKill(npcs.ExampleSlimeNPC);
    if (frames === 100) {
        const gel = count(GEL), ex = count(items.ExampleItem);
        bl.log('drops 30 slimes: gel ' + gel + ', Exemplo de Item ' + ex);
        check('slime solta gel (item do jogo)', () => gel >= 30 || 'gel ' + gel);
        check('slime solta Exemplo de Item (item de mod)', () => ex > 0 || 'nenhum em 30 mortes');
        Main.dayTime = false;
        exBefore = ex;
        boss = spawnAndKill(npcs.ExampleBoss);
        bl.log('drops chefe apos o golpe: ativo=' + boss.active + ' vida=' + boss.life + '/' + boss.lifeMax);
    }
    if (frames === 110) {
        done = true;
        const ex = count(items.ExampleItem) - exBefore;
        bl.log('drops chefe: Exemplo de Item ' + ex);
        check('chefe solta 15 a 30 Exemplos de Item', () => (ex >= 15 && ex <= 30) || 'caiu ' + ex);
        bl.log('drops FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('drops: carregado');
