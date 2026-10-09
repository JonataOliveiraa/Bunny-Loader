// O ícone de chefe de mod (o `_Head_Boss` ao lado da textura): o ExampleBoss
// do Example Mod ganha uma vaga no fim de TextureAssets.NpcHeadBoss e o
// índice em NPCID.Sets.BossHeadTextures. A barra de chefe do jogo
// (BigProgressBarSystem) e os ícones do mapa leem NPC.GetBossHeadTextureIndex:
// com o índice, o chefe ganha a barra grande. Também a 2ª cabeça
// (Mod.AddBossHeadTexture + BossHeadSlot) e o _BestiaryIcon no Bestiário.
// Loga "bosshead <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { NPCID } = Terraria.ID;
const TextureAssets = Terraria.GameContent.TextureAssets;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('bosshead ' + label + ': ok');
        else { fails++; bl.log('bosshead ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('bosshead ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const me = () => Main.player[Main.myPlayer];

let bossType = -1, slot = -1, boss = null;
let frames = 0, started = -1, done = false;

function finish() {
    if (boss && boss.active) boss.active = false;
    done = true;
    bl.log('bosshead FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;

    const p = me();
    p.statLife = p.statLifeMax2;
    if (started < 0) {
        started = frames;
        for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
            const m = ModNPC.getModNPC(t);
            if (m && m.constructor.name === 'ExampleBoss') bossType = t;
        }
        check('preparo: ExampleBoss registrado', () => bossType > 0 || 'sem o ExampleBoss');
        if (bossType < 0) return finish();

        slot = NPCID.Sets.BossHeadTextures[bossType];
        const heads = TextureAssets.NpcHeadBoss;
        bl.log(`bosshead diag: tipo ${bossType}, BossHeadTextures ${slot}, NpcHeadBoss ${heads.length}`);
        check('o tipo de mod tem índice de ícone', () => slot >= 0 || 'índice ' + slot);
        check('a vaga fica no fim de NpcHeadBoss, depois das do jogo', () => {
            let vanillaMax = -1;
            for (let t = 0; t < bl.npcs.vanillaCount; t++) vanillaMax = Math.max(vanillaMax, NPCID.Sets.BossHeadTextures[t]);
            return (slot > vanillaMax && slot < heads.length) || `${slot}, maior do jogo ${vanillaMax}, ${heads.length} vagas`;
        });
        check('a textura da vaga é o PNG (26x28)', () => {
            const tex = heads[slot].Value;
            return (tex.Width === 26 && tex.Height === 28) || `${tex.Width}x${tex.Height}`;
        });
        check('o do jogo continua igual (Olho de Cthulhu = 0)', () => NPCID.Sets.BossHeadTextures[NPCID.EyeofCthulhu] === 0 || NPCID.Sets.BossHeadTextures[NPCID.EyeofCthulhu]);
        check('NPC de mod sem _Head_Boss continua -1', () => {
            for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
                const m = ModNPC.getModNPC(t);
                if (m && m.constructor.name === 'ExampleSlimeNPC') return NPCID.Sets.BossHeadTextures[t] === -1 || NPCID.Sets.BossHeadTextures[t];
            }
            return 'sem o ExampleSlimeNPC';
        });

        // A 2ª fase (Mod.AddBossHeadTexture no SetStaticDefaults do ExampleBoss).
        const second = ModContent.GetModBossHeadSlot('examplemod/Content/NPCs/ExampleBoss/ExampleBoss_Head_Boss_2');
        bl.log(`bosshead diag: 2a fase na vaga ${second}`);
        check('AddBossHeadTexture reservou a 2a cabeça depois da padrão', () => second > slot || `${second} (padrão ${slot})`);
        check('a 2a cabeça é o PNG dela (26x28) e não a padrão', () => {
            const a = heads[second], b = heads[slot];
            return (a.Value.Width === 26 && bl.addressOf(a) !== bl.addressOf(b)) || `${a.Value.Width}x${a.Value.Height}`;
        });
        check('arquivo que não existe: -1', () => ModContent.GetModBossHeadSlot('examplemod/nada_Head_Boss') === -1);

        // O ícone do Bestiário (ExampleBoss_BestiaryIcon.png, 240x160) no
        // entry.Icon, que a lista e o retrato desenham.
        const db = Main.BestiaryDB;
        const find = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'];
        check('BestiaryIcon: o ExampleBoss usa o _BestiaryIcon', () => {
            const custom = find(bossType).Icon._customTexture;
            return (!!custom && custom.Value.Width === 240 && custom.Value.Height === 160) || (custom ? `${custom.Value.Width}x${custom.Value.Height}` : 'nulo');
        });
        check('BestiaryIcon: NPC sem o arquivo continua desenhando o NPC', () => {
            for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
                const m = ModNPC.getModNPC(t);
                if (m && m.constructor.name === 'ExampleSlimeNPC') return !find(t).Icon._customTexture || 'tem textura';
            }
            return 'sem o ExampleSlimeNPC';
        });
        check('BestiaryIcon: o do jogo continua sem (Olho de Cthulhu)', () => !find(NPCID.EyeofCthulhu).Icon._customTexture || 'tem textura');

        Main.dayTime = false;
        Main.time = 1000;
        const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 10 * 16,
                           Math.floor(p.position.Y) - 6 * 16, bossType, 0, 0, 0, 0, 0, Main.myPlayer);
        boss = Main.npc[idx];
        boss.damage = 0;
        return;
    }

    const f = frames - started;
    if (boss && boss.active) boss.damage = 0;
    if (f < 90) return;

    check('o chefe vivo devolve o índice (GetBossHeadTextureIndex)', () =>
        (boss.active && boss.GetBossHeadTextureIndex() === slot) || `ativo ${boss.active}, índice ${boss.GetBossHeadTextureIndex()}`);
    check('2a fase (ai[0] = 1): BossHeadSlot troca a cabeça', () => {
        const second = ModContent.GetModBossHeadSlot('examplemod/Content/NPCs/ExampleBoss/ExampleBoss_Head_Boss_2');
        boss.ai[0] = 1;
        const now = boss.GetBossHeadTextureIndex();
        boss.ai[0] = 0;
        return now === second || `índice ${now}, esperado ${second}`;
    });
    check('o jogo mostra a barra de chefe dele', () => {
        const bar = Main.BigBossProgressBar._currentBar;
        bl.log('bosshead diag: barra atual ' + (bar ? bar.GetType().Name : 'nenhuma'));
        return !!bar || 'sem barra';
    });
    finish();
});
bl.log('bosshead: carregado');

export default class TestBossHead extends Mod {}
