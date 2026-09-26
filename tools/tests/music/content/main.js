// Musica de mod (MusicLoader, ModNPC.Music). Loga "music ...". Precisa do
// Example Mod: o chefe dele (ExampleBoss) tem Music = Music/Ropocalypse2.
//
// Carga: GetMusicSlot da um slot depois dos do jogo, o mesmo para o mesmo
// arquivo (com ou sem extensao, pelo Mod ou pelo caminho inteiro; e o do
// chefe, pedido pelo Example Mod), e 0 para arquivo que nao existe.
// No mundo: o chefe nasce perto; a musica do jogo vai a 0 e a dele toca.
// Fase 2: o Music dele vira um MusicID do jogo (Boss2); o jogo toca esse e a
// do mod sai. Ele some; a do jogo volta a de sempre.
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

let frames = 0, done = false, boss = null, bossMusic = 0, vanillaBefore = 0, savedTime = null;
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
    if (frames === 60) {
        vanillaBefore = Main.newMusic;
        // De dia o chefe foge (EncourageDespawn): noite enquanto ele esta aqui.
        savedTime = { day: Main.dayTime, time: Main.time };
        Main.dayTime = false;
        Main.time = 0;
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
    if (frames === 150) {
        check('com o chefe perto: a do jogo vai a 0 e a dele toca', () =>
            (Main.newMusic === 0 && MusicLoader.IsMusicPlaying(bossMusic)) ||
            `newMusic ${Main.newMusic}, tocando ${MusicLoader.IsMusicPlaying(bossMusic)}`);
    }
    if (frames === 160 && boss) boss.ModNPC.Music = Terraria.ID.MusicID.Boss2;
    if (frames === 300) {
        check('fase 2 com uma musica do jogo: o jogo toca ela, a do mod para', () =>
            (Main.newMusic === Terraria.ID.MusicID.Boss2 && !MusicLoader.IsMusicPlaying(bossMusic)) ||
            `newMusic ${Main.newMusic} (Boss2 ${Terraria.ID.MusicID.Boss2}), mod tocando ${MusicLoader.IsMusicPlaying(bossMusic)}`);
    }
    if (frames === 310 && boss) {
        boss.active = false;
        boss = null;
        Main.dayTime = savedTime.day;
        Main.time = savedTime.time;
    }
    if (frames === 420) {
        check('sem o chefe: a dele para e a do jogo volta', () =>
            (!MusicLoader.IsMusicPlaying(bossMusic) && Main.newMusic > 0 && Main.newMusic !== Terraria.ID.MusicID.Boss2) ||
            `tocando ${MusicLoader.IsMusicPlaying(bossMusic)}, newMusic ${Main.newMusic} (antes ${vanillaBefore})`);
        done = true;
        bl.log('music FIM ' + (fails ? fails + ' falha(s)' : 'ok'));
    }
});
bl.log('music: carregado');
