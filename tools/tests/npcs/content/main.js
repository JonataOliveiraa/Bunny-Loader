// Teste dos NPCs de mod (runtime/ModNpcs.cpp). Precisa do Example Mod ligado:
// ele registra o ExampleSlimeNPC, o primeiro NPC de mod (tipo 697 = NPCID.Count).
//
// Invoca o slime ao lado do jogador e confere: nasce ativo com os valores do
// setDefaults do mod e a vida calculada DEPOIS dele; tem nome; anima (o quadro
// muda: animationType) e se mexe (aiStyle de slime); morre, conta no Bestiario e
// da o drop da tabela (gel sempre). Loga "npcs <caso>: ok | FALHOU".
const Main = Terraria.Main;
const SLIME = 697;
const GEL = Terraria.ID.ItemID.Gel;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, ' +
                            'float ai0, float ai1, float ai2, float ai3, int Target)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('npcs ' + label + ': ok');
        else { fails++; bl.log('npcs ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('npcs ' + label + ': FALHOU com ' + e);
    }
}

let npc = null, startX = 0;
const framesSeen = new Set();
let maxSpeed = 0;

function gelNear(x, y) {
    let n = 0;
    const items = Main.item;
    for (let i = 0; i < items.length; i++) {
        const it = items[i];
        if (it.active && it.type === GEL && Math.abs(it.position.X - x) < 300 && Math.abs(it.position.Y - y) < 300) {
            n += it.stack;
        }
    }
    return n;
}

function spawn() {
    check('registrado', () => bl.npcs.isModNpc(SLIME) || 'isModNpc(697) = false');
    check('nasce', () => {
        const p = Main.player[Main.myPlayer];
        const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
        source['void .ctor()']();
        startX = p.position.X + 160;
        const i = newNpc(source, Math.floor(startX), Math.floor(p.position.Y), SLIME, 0, 0, 0, 0, 0, 255);
        npc = Main.npc[i];
        bl.log(`npcs estado em [${i}]: ativo=${npc.active} tipo=${npc.type} vida=${npc.life}/${npc.lifeMax} ` +
               `dano=${npc.damage} defesa=${npc.defense} tamanho=${npc.width}x${npc.height} aiStyle=${npc.aiStyle}`);
        if (!npc.active || npc.type !== SLIME) return `ativo=${npc.active} tipo=${npc.type}`;
        if (npc.lifeMax < 25 || npc.life !== npc.lifeMax) return `vida ${npc.life}/${npc.lifeMax}`;
        if (npc.damage < 7 || npc.aiStyle !== 1) return `dano=${npc.damage} aiStyle=${npc.aiStyle}`;
    });
    // Diagnostico do desenho: a textura (a nossa e a do slime azul, 1) e o quadro.
    for (const t of [SLIME, 1]) {
        const a = Terraria.GameContent.TextureAssets.Npc[t];
        const v = a.Value;
        bl.log(`npcs textura[${t}]: carregada=${a.IsLoaded} ${v === null ? 'Value=null' : v.Width + 'x' + v.Height} ` +
               `quadros=${Main.npcFrameCount[t]}`);
    }
    bl.log(`npcs quadro=${npc.frame.X},${npc.frame.Y},${npc.frame.Width}x${npc.frame.Height} alpha=${npc.alpha} ` +
           `cor=${npc.color.R},${npc.color.G},${npc.color.B},${npc.color.A} escala=${npc.scale}`);
    check('nome', () => {
        const n = Terraria.Lang['string GetNPCNameValue(int netID)'](SLIME);
        return /Slime de Exemplo|Example Slime/.test(n) || 'nome=' + n;
    });
}

function watch() {
    if (!npc || !npc.active) return;
    if (frames % 60 === 0) {
        const sp = Main.screenPosition, me = Main.player[Main.myPlayer];
        bl.log(`npcs na tela: slime (${(npc.position.X - sp.X).toFixed(0)}, ${(npc.position.Y - sp.Y).toFixed(0)}) ` +
               `jogador (${(me.position.X - sp.X).toFixed(0)}, ${(me.position.Y - sp.Y).toFixed(0)}) ` +
               `vida jogador ${me.statLife} morto=${me.dead}`);
    }
    framesSeen.add(npc.frame.Y);
    maxSpeed = Math.max(maxSpeed, Math.abs(npc.velocity.X) + Math.abs(npc.velocity.Y));
}

let gelBefore = 0, deathX = 0, deathY = 0, killsBefore = 0;
function kill() {
    killsBefore = Main.BestiaryTracker.Kills['int GetKillCount(NPC npc)'](npc);
    check('anima', () => framesSeen.size >= 2 || 'quadros vistos: ' + [...framesSeen].join(','));
    check('se mexe', () => maxSpeed > 0.5 || 'velocidade max ' + maxSpeed.toFixed(2));
    deathX = npc.position.X;
    deathY = npc.position.Y;
    gelBefore = gelNear(deathX, deathY);
    // Golpe DO jogador: o Bestiario so credita morte com participacao de um.
    npc.playerInteraction[Main.myPlayer] = true;
    npc['double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)'](
        9999, 0, 1, false, false, false, Main.myPlayer);
}

function afterDeath() {
    check('morre', () => !npc.active || 'ainda ativo com vida ' + npc.life);
    // No Bestiario (NPC.killCount e por ESTANDARTE, e o slime nao tem um).
    // Era aqui que o jogo lancaria excecao sem a amostra registrada.
    check('conta a morte no Bestiario', () => {
        const n = Main.BestiaryTracker.Kills['int GetKillCount(NPC npc)'](npc);
        return n >= killsBefore + 1 || `mortes ${killsBefore} -> ${n}`;
    });
    check('drop (gel)', () => {
        const n = gelNear(deathX, deathY) - gelBefore;
        return n >= 1 || 'gel novo perto: ' + n;
    });
    bl.log('npcs FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 60) spawn();
    if (frames > 60 && frames < 300) watch();
    if (frames === 300) kill();
    if (frames === 310) afterDeath();
});
bl.log('npcs: carregado');
