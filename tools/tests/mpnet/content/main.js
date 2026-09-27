// A rede dos mods entre host e cliente:
//   - ModSystem.NetSend/NetReceive: vai com os dados do mundo (ao entrar, e de
//     novo quando o host manda a mensagem 7);
//   - ModPacket: o cliente manda um ping, o host responde so a ele;
//   - GlobalNPC.NetSend: o estado de um slime criado no host chega ao cliente;
//   - GlobalProjectile.NetSend: o estado da flecha do cliente chega ao host
//     (o servidor repassa).
// Instale no host e no cliente, com o Example Mod. Loga "mpn <papel> ...".
const Main = Terraria.Main;
const { NPCID, ProjectileID } = Terraria.ID;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, ' +
                            'float ai0, float ai1, float ai2, float ai3, int Target)'];
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, ' +
    'float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, ' +
    'NewProjectileModifier modifer)'];
const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];

const net = { world: [], pings: [], pongs: [] };

class NetWorld extends ModSystem {
    static secret = 0;
    static label = '';
    NetSend(writer) {
        writer.Write(NetWorld.secret);
        writer.Write(NetWorld.label);
        writer.WriteFlags(true, false, true);
        writer.WriteVector2(Vector2.new(3, 4));
    }
    NetReceive(reader) {
        const secret = reader.ReadInt32();
        const label = reader.ReadString();
        const flags = reader.ReadFlags();
        const v = reader.ReadVector2();
        net.world.push({ secret, label, flags, x: v.X, y: v.Y });
    }
}
ModSystem.register(NetWorld);

let me = null;

export default class NetTestMod extends Mod {
    Load() { me = this; }

    HandlePacket(reader, whoAmI) {
        const kind = reader.ReadString();
        const n = reader.ReadInt32();
        if (kind === 'ping') {
            net.pings.push({ n, from: whoAmI });
            const p = this.GetPacket();
            p.Write('pong');
            p.Write(n + 1);
            p.Send(whoAmI);
        } else if (kind === 'pong') {
            net.pongs.push({ n, from: whoAmI });
        }
    }
}
class NetSlime extends GlobalNPC {
    InstancePerEntity = true;
    mark = 0;
    AppliesToEntity(npc, lateInstantiation) { return npc.type === NPCID.BlueSlime; }
    NetSend(npc, writer) { writer.Write(this.mark); }
    NetReceive(npc, reader) { this.mark = reader.ReadInt32(); }
}
GlobalNPC.register(NetSlime);

class NetArrow extends GlobalProjectile {
    InstancePerEntity = true;
    mark = '';
    AppliesToEntity(p, lateInstantiation) { return p.type === ProjectileID.WoodenArrowFriendly; }
    NetSend(p, writer) { writer.Write(this.mark); }
    NetReceive(p, reader) { this.mark = reader.ReadString(); }
}
GlobalProjectile.register(NetArrow);

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpn ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpn ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpn ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpn ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
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
function nearestNpc(type, maxDist) {
    const pos = Main.player[Main.myPlayer].position;
    let best = null, bestDist = maxDist;
    for (let i = 0; i < 200; i++) {
        const n = Main.npc[i];
        if (!n.active || n.type !== type) continue;
        const d = Math.hypot(n.position.X - pos.X, n.position.Y - pos.Y);
        if (d < bestDist) { best = n; bestDist = d; }
    }
    return best;
}
function arrowOf(owner) {
    for (let i = 0; i < 1000; i++) {
        const p = Main.projectile[i];
        if (p.active && p.type === ProjectileID.WoodenArrowFriendly && p.owner === owner) return p;
    }
    return null;
}

// NetWorld.secret e o que o host manda. Ao carregar o mundo, antes de o
// cliente entrar: o primeiro envio (a mensagem 7 da entrada) ja leva ele.
NetWorld.secret = 42;
NetWorld.label = 'mundo do host';

// -------------------------------- host --------------------------------
let started = -1, slime = null, arrowMark = null, resentAt = -1;
function hostTick(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (started < 0) {
        started = frames;
        bl.log('mpn host: cliente entrou (' + Main.player[r].name + ')');
        return;
    }
    const t = frames - started;
    if (t === 120) {
        const c = Main.player[r];
        const i = newNpc(debugSource(), Math.floor(c.position.X + 120), Math.floor(c.position.Y - 60),
                         NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
        slime = Main.npc[i];
        slime.GetGlobalNPC(NetSlime).mark = 777;
        sendData(23, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
    }
    if (t === 240) {
        // O mundo muda: o host manda a mensagem 7 de novo, e os dados do mod vao junto.
        NetWorld.secret = 43;
        NetWorld.label = 'mudou';
        sendData(7, -1, -1, null, 0, 0, 0, 0, 0, 0, 0);
        resentAt = t;
    }
    if (arrowMark === null) {
        const a = arrowOf(r);
        if (a) {
            const g = a.GetGlobalProjectile(NetArrow);
            if (g.mark) arrowMark = g.mark;
        }
    }
    if ((arrowMark !== null && net.pings.length && t > 300) || t > 2400) {
        if (slime && slime.active) slime.active = false;
        check('ping do cliente chega ao host, com o indice dele', () =>
            (net.pings.length === 1 && net.pings[0].n === 5 && net.pings[0].from === r) || JSON.stringify(net.pings));
        check('GlobalProjectile.NetSend: a marca da flecha do cliente chega ao host', () =>
            arrowMark === 'do cliente' || 'marca ' + arrowMark);
        check('o host nao recebe os dados do mundo (ele e quem manda)', () => net.world.length === 0 || JSON.stringify(net.world));
        finish();
    }
}

// ------------------------------- cliente -------------------------------
let pinged = -1, sawSlime = null, shot = -1;
function clientTick(frames) {
    if (frames === 60) {
        check('NetReceive do mundo ao entrar', () => {
            const w = net.world[0];
            return (w && w.secret === 42 && w.label === 'mundo do host' && w.flags.join() === 'true,false,true' &&
                    w.x === 3 && w.y === 4) || JSON.stringify(net.world);
        });
        const p = me.GetPacket();
        p.Write('ping');
        p.Write(5);
        p.Send();
        pinged = frames;
    }
    if (!sawSlime) {
        const s = nearestNpc(NPCID.BlueSlime, 400);
        if (s && s.GetGlobalNPC(NetSlime).mark === 777) sawSlime = s;
    }
    if (sawSlime && shot < 0) {
        const pos = Main.player[Main.myPlayer].position;
        const i = newProj(debugSource(), pos.X, pos.Y - 120, 0, -6, ProjectileID.WoodenArrowFriendly,
                          1, 0, Main.myPlayer, 0, 0, 0, null);
        Main.projectile[i].GetGlobalProjectile(NetArrow).mark = 'do cliente';
        sendData(27, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
        shot = frames;
    }
    const worldAgain = net.world.some((w) => w.secret === 43);
    if ((shot >= 0 && worldAgain && frames - shot > 60) || frames > 2400) {
        check('ModPacket: o pong do host chega so a quem pediu, vindo do servidor (256)', () =>
            (net.pongs.length === 1 && net.pongs[0].n === 6 && net.pongs[0].from === 256) || JSON.stringify(net.pongs));
        check('GlobalNPC.NetSend: a marca do slime do host chega ao cliente', () =>
            sawSlime !== null || 'slime sem a marca');
        check('NetReceive de novo quando o host manda o mundo', () =>
            (worldAgain && net.world.find((w) => w.secret === 43).label === 'mudou') || JSON.stringify(net.world));
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
        bl.log(`mpn: netMode ${mode}, papel ${role || 'nenhum'}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) self.statLife = self.statLifeMax2;
    if (role === 'host') hostTick(frames);
    else clientTick(frames);
});
bl.log('mpn: carregado');
