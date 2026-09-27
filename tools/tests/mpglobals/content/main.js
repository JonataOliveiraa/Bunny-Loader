// Globais e ModSystem no multijogador, dos dois lados. O host cria um slime
// azul perto do cliente; o cliente bate nele (golpe local: o slime morre so
// na tela dele, e o OnKill nao pode rodar no cliente); o host mata o dele; o
// cliente atira uma flecha. Cada lado
// confere o que roda nele:
//   - SetDefaults dos Globais: nos dois (o cliente monta o NPC e o projetil
//     que chegam pela rede);
//   - OnSpawn: so de quem criou (host no slime, cliente na flecha);
//   - HitEffect: nos dois; OnKill: so no host (o drop e do servidor);
//   - ModSystem: LoadWorldData e UpdateWorld so no host; OnWorldLoad e
//     PostUpdateEverything nos dois.
// Instale no host e no cliente, com o Example Mod. Loga "mpg <papel> ...".
const Main = Terraria.Main;
const { NPCID, ProjectileID } = Terraria.ID;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, ' +
                            'float ai0, float ai1, float ai2, float ai3, int Target)'];
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, ' +
    'float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, ' +
    'NewProjectileModifier modifer)'];
const strike = 'double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)';
const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];

const spy = {
    slimeSpawn: 0, slimeHit: 0, slimeKill: 0, arrowMine: 0, arrowOthers: 0,
    worldLoad: 0, loadData: 0, updateWorld: 0, everything: 0,
};

class MpSlime extends GlobalNPC {
    AppliesToEntity(npc, lateInstantiation) { return npc.type === NPCID.BlueSlime; }
    SetDefaults(npc) {
        npc.lifeMax = 777;
        npc.life = 777;
    }
    OnSpawn(npc, source) { spy.slimeSpawn++; }
    HitEffect(npc, hitDirection, damage) { spy.slimeHit++; }
    OnKill(npc) { spy.slimeKill++; }
}
GlobalNPC.register(MpSlime);

class MpArrow extends GlobalProjectile {
    AppliesToEntity(p, lateInstantiation) { return p.type === ProjectileID.WoodenArrowFriendly; }
    SetDefaults(p) { p.scale = 2; }
    // No host, o Guia atira flecha de madeira como se fosse do jogador local:
    // conta as minhas e as de OUTRO jogador separadas.
    OnSpawn(p, source) {
        if (p.owner === Main.myPlayer) spy.arrowMine++;
        else if (p.owner !== 255) spy.arrowOthers++;
    }
}
GlobalProjectile.register(MpArrow);

class MpWorld extends ModSystem {
    OnWorldLoad() { spy.worldLoad++; }
    LoadWorldData(tag) { spy.loadData++; }
    PostUpdateWorld() { spy.updateWorld++; }
    PostUpdateEverything() { spy.everything++; }
}
ModSystem.register(MpWorld);

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpg ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpg ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpg ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpg ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}
function debugSource() {
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    return source;
}
function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}
// O slime mais perto de mim: o host cria o dele ao meu lado (os outros sao
// do mundo).
function nearestNpc(type, maxDist) {
    const me = Main.player[Main.myPlayer].position;
    let best = null, bestDist = maxDist;
    for (let i = 0; i < 200; i++) {
        const n = Main.npc[i];
        if (!n.active || n.type !== type) continue;
        const d = Math.hypot(n.position.X - me.X, n.position.Y - me.Y);
        if (d < bestDist) { best = n; bestDist = d; }
    }
    return best;
}
function findArrow(owner) {
    for (let i = 0; i < 1000; i++) {
        const p = Main.projectile[i];
        if (p.active && p.type === ProjectileID.WoodenArrowFriendly && p.owner === owner) return p;
    }
    return null;
}

// -------------------------------- host --------------------------------
let started = -1, slime = null, arrowScale = -1, slimeDead = false;
function hostTick(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (started < 0) {
        started = frames;
        bl.log('mpg host: cliente entrou (' + Main.player[r].name + ')');
        return;
    }
    const t = frames - started;
    if (t === 120) {
        const c = Main.player[r];
        const before = spy.slimeSpawn;
        const i = newNpc(debugSource(), Math.floor(c.position.X + 120), Math.floor(c.position.Y - 60),
                         NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
        slime = Main.npc[i];
        sendData(23, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
        check('OnSpawn do slime roda no host, e o SetDefaults', () =>
            (spy.slimeSpawn - before === 1 && slime.lifeMax === 777) || `spawn ${spy.slimeSpawn - before}, vida ${slime.lifeMax}`);
    }
    if (t === 360 && slime) {
        const kills = spy.slimeKill;
        slime.playerInteraction[r] = true;
        slime[strike](99999, 0, 1, false, false, false, r);
        slimeDead = true;
        check('matar no host: HitEffect e OnKill', () =>
            (spy.slimeHit > 0 && spy.slimeKill - kills === 1) || `hit ${spy.slimeHit}, kill ${spy.slimeKill - kills}`);
    }
    if (arrowScale < 0) {
        const a = findArrow(r);
        if (a) arrowScale = a.scale;
    }
    if ((slimeDead && arrowScale >= 0) || t > 2400) {
        check('flecha do cliente chega com o SetDefaults do host', () => arrowScale === 2 || 'escala ' + arrowScale);
        check('OnSpawn da flecha do cliente nao roda no host', () => spy.arrowOthers === 0 || 'spawn ' + spy.arrowOthers);
        check('ModSystem no host: OnWorldLoad, LoadWorldData, UpdateWorld, UpdateEverything', () =>
            (spy.worldLoad > 0 && spy.loadData === 1 && spy.updateWorld > 0 && spy.everything > 0) ||
            JSON.stringify(spy));
        finish();
    }
}

// ------------------------------- cliente -------------------------------
let sawSlime = null, slimeLife = -1, slimeGone = -1, shotAt = -1, myArrow = null;
function clientTick(frames) {
    if (!sawSlime) {
        const s = nearestNpc(NPCID.BlueSlime, 400);
        if (s) {
            sawSlime = s;
            slimeLife = s.lifeMax;
            bl.log('mpg cliente: slime chegou');
            // Golpe local, sem avisar o servidor: o NPCLoot do jogo sai logo
            // no cliente, e o OnKill tem de sair junto.
            s.playerInteraction[Main.myPlayer] = true;
            s[strike](99999, 0, 1, false, false, false, Main.myPlayer);
        }
        if (frames > 2400) {
            check('o slime do host chega', () => 'nao chegou');
            finish();
        }
        return;
    }
    if (slimeGone < 0 && frames > 3000) {
        check('o host mata o slime', () => 'o slime nao morreu');
        finish();
        return;
    }
    if (slimeGone < 0 && !sawSlime.active) {
        slimeGone = frames;
        check('slime chega com o SetDefaults do cliente', () => slimeLife === 777 || 'vida ' + slimeLife);
        check('OnSpawn do slime nao roda no cliente', () => spy.slimeSpawn === 0 || 'spawn ' + spy.slimeSpawn);
        check('golpe local no cliente: HitEffect roda, OnKill nao (o drop e do servidor)', () =>
            (spy.slimeHit > 0 && spy.slimeKill === 0) || `hit ${spy.slimeHit}, kill ${spy.slimeKill}`);
    }
    if (slimeGone >= 0 && shotAt < 0 && frames - slimeGone > 30) {
        const me = Main.player[Main.myPlayer];
        const i = newProj(debugSource(), me.position.X, me.position.Y - 120, 0, -6,
                          ProjectileID.WoodenArrowFriendly, 1, 0, Main.myPlayer, 0, 0, 0, null);
        myArrow = Main.projectile[i];
        sendData(27, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
        shotAt = frames;
        check('flecha do cliente: OnSpawn e SetDefaults', () =>
            (spy.arrowMine === 1 && myArrow.scale === 2) || `spawn ${spy.arrowMine}, escala ${myArrow.scale}`);
    }
    if (shotAt >= 0 && frames - shotAt > 90) {
        check('ModSystem no cliente: OnWorldLoad e UpdateEverything, sem LoadWorldData nem UpdateWorld', () =>
            (spy.worldLoad > 0 && spy.everything > 0 && spy.loadData === 0 && spy.updateWorld === 0) ||
            JSON.stringify(spy));
        finish();
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mpg: netMode ${mode}, papel ${role || 'nenhum'}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) self.statLife = self.statLifeMax2;
    if (role === 'host') hostTick(frames);
    else clientTick(frames);
});
bl.log('mpg: carregado');
