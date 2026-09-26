// Musica de mod (MusicLoader, ModNPC.Music). Loga "music ...". Precisa do
// Example Mod: o chefe dele (ExampleBoss) tem Music = Music/Ropocalypse2.
//
// Carga: GetMusicSlot da um slot depois dos do jogo, o mesmo para o mesmo
// arquivo (com ou sem extensao, pelo Mod ou pelo caminho inteiro; e o do
// chefe, pedido pelo Example Mod), e 0 para arquivo que nao existe.
// No mundo, cada troca e conferida no meio, como a do jogo entre duas faixas
// (a nova sobe 0,005 por quadro; a velha segura ate a nova passar de 0,25 e
// depois desce no mesmo passo):
//   - o chefe chega: a do jogo segura enquanto a dele entra, depois sai aos
//     poucos (sem corte) e ele fica sozinho;
//   - fase 2, o Music vira um MusicID do jogo (Boss2): a do jogo entra e a
//     dele segura ate ela se ouvir, depois sai;
//   - o Music volta a ser o dele, e a luta acaba (o chefe some): a do jogo
//     volta e a dele segura ate ela se ouvir, depois sai.
const Main = Terraria.Main;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('music ' + label + ': ok');
        else { fails++; bl.log('music ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('music ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const exampleMod = ModLoader.GetMod('examplemod');
let slot = 0;
check('GetMusicSlot', () => {
    slot = MusicLoader.GetMusicSlot(exampleMod, 'Music/Ropocalypse2');
    if (slot < Terraria.ID.MusicID.Count) return 'slot ' + slot;
    if (MusicLoader.GetMusicSlot(exampleMod, 'Music/Ropocalypse2.ogg') !== slot) return 'com .ogg deu outro';
    if (MusicLoader.GetMusicSlot(exampleMod.path + '/Music/Ropocalypse2') !== slot) return 'caminho inteiro deu outro';
    return (MusicLoader.MusicCount > slot) || 'MusicCount ' + MusicLoader.MusicCount;
});
check('arquivo que nao existe da 0', () =>
    (MusicLoader.GetMusicSlot('Music/nao-existe') === 0 && !MusicLoader.MusicExists('Music/nao-existe') &&
     MusicLoader.MusicExists(exampleMod, 'Music/Ropocalypse2')) || 'nao');

let frames = 0, done = false, boss = null, bossMusic = 0, old = 0, savedTime = null;
const fadeOf = (music) => Terraria.Main.musicFade[music];
const playing = () => MusicLoader.IsMusicPlaying(bossMusic);
// Os NPCs de mod com Music no mundo agora: "slot:tipo:Music".
function musicNpcsNow() {
    const out = [];
    for (let i = 0; i < Main.npc.length; i++) {
        const n = Main.npc[i];
        const m = n.active ? n.ModNPC : null;
        if (m && (m.Music | 0) >= 0) out.push(`${i}:${n.type}:${m.Music}`);
    }
    return out.join(' ') || 'nenhum';
}
const state = () => `newMusic ${Main.newMusic}, cur ${Main.curMusic}, fade da velha (${old}) ` +
    `${fadeOf(old).toFixed(3)}, fade Boss2 ${fadeOf(Terraria.ID.MusicID.Boss2).toFixed(3)}, dele tocando ${playing()}, ` +
    `NPCs com musica: ${musicNpcsNow()}`;

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer) return;
    frames++;
    const p = self;
    if (boss) {   // o chefe bate: o jogador de teste nao pode morrer no meio
        p.immune = true;
        p.immuneTime = 60;
        p.statLife = p.statLifeMax2;
    }
    // De dia o chefe foge (EncourageDespawn): noite desde o comeco, para a
    // musica da noite ja estar inteira quando ele chegar.
    if (frames === 30) {
        savedTime = { day: Main.dayTime, time: Main.time };
        Main.dayTime = false;
        Main.time = 0;
    }
    if (frames === 300) {
        old = Main.curMusic;
        check('antes do chefe: a do jogo inteira', () => (old > 0 && fadeOf(old) >= 0.99) || state());
        check('o chefe do Example Mod nasce perto', () => {
            // O getTypeByName procura so no mod de quem chama: o chefe e de outro.
            let type = -1;
            for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
                const m = ModNPC.getModNPC(t);
                if (m && m.constructor.name === 'ExampleBoss') type = t;
            }
            if (!(type > 0)) return 'sem ExampleBoss (o Example Mod esta ligado?)';
            const src = Terraria.DataStructures.EntitySource_DebugCommand.new();
            boss = Main.npc[newNpc(src, Math.floor(p.Center.X), Math.floor(p.Center.Y) - 300, type, 0, 0, 0, 0, 0, Main.myPlayer)];
            bossMusic = boss.ModNPC ? boss.ModNPC.Music : -1;
            // O mesmo arquivo: o mesmo slot, pedido pelo chefe ou por este teste.
            return (boss.active && bossMusic === slot) || `ativo ${boss.active}, Music ${bossMusic}, slot ${slot}`;
        });
    }
    if (frames === 330) {
        check('o chefe chega: a do jogo segura enquanto a dele entra', () =>
            (Main.newMusic === old && fadeOf(old) >= 0.99 && playing()) || state());
    }
    if (frames === 380) {
        check('o chefe chega: a do jogo sai aos poucos, sem corte', () =>
            (Main.newMusic === 0 && fadeOf(old) > 0.5 && fadeOf(old) < 0.99 && playing()) || state());
    }
    if (frames === 600) {
        check('com o chefe: so a dele', () => (Main.newMusic === 0 && fadeOf(old) === 0 && playing()) || state());
        boss.ModNPC.Music = Terraria.ID.MusicID.Boss2;
    }
    if (frames === 630) {
        check('fase 2 com uma musica do jogo: ela entra e a dele segura', () =>
            (Main.newMusic === Terraria.ID.MusicID.Boss2 && fadeOf(Terraria.ID.MusicID.Boss2) < 0.25 && playing()) || state());
    }
    if (frames === 900) {
        check('fase 2: a dele saiu, a do jogo inteira', () =>
            (!playing() && fadeOf(Terraria.ID.MusicID.Boss2) >= 0.99) || state());
        boss.ModNPC.Music = bossMusic;
    }
    if (frames === 1200) {
        check('a dele de volta, a do jogo fora', () =>
            (Main.newMusic === 0 && playing() && fadeOf(Terraria.ID.MusicID.Boss2) < 0.1) || state());
        boss.active = false;   // o fim da luta
        boss = null;
    }
    if (frames === 1230) {
        check('fim da luta: a do jogo volta e a dele segura', () =>
            (Main.newMusic > 0 && Main.newMusic !== Terraria.ID.MusicID.Boss2 && fadeOf(Main.newMusic) < 0.25 && playing()) || state());
    }
    if (frames === 1500) {
        check('fim da luta: a dele saiu, a do jogo inteira', () =>
            (!playing() && Main.curMusic > 0 && fadeOf(Main.curMusic) >= 0.99) || state());
        Main.dayTime = savedTime.day;
        Main.time = savedTime.time;
        done = true;
        bl.log('music FIM ' + (fails ? fails + ' falha(s)' : 'ok'));
    }
});
bl.log('music: carregado');
