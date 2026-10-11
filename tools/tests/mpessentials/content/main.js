// Coleta, gancho e ShouldUpdatePosition no multijogador (anfitrião que joga +
// um cliente), num piso com teto no alto, longe do spawn (o anfitrião faz).
//   - Coleta: o servidor solta os itens junto do cliente parado no piso. A
//     reserva (WorldItem.FindOwner) roda no servidor e a coleta no cliente:
//     OnPickup false consome e some dos dois lados; CanPickup false fica nos
//     dois; GrabRange +400 puxa de 20 blocos (a reserva usa o alcance do mod);
//     a madeira a 12 blocos fica. Com o inventário do cliente cheio (mandado
//     ao servidor, mensagem 5), ItemSpace true pega e um item comum não.
//   - Gancho: o cliente prende 6 ganchos com NumGrappleHooks 5; o mais velho
//     solta e o servidor fica com 5.
//   - ShouldUpdatePosition false: a cópia do servidor não anda; a de controle anda.
// Loga "mpess <lado> <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { ItemID, TileID, ProjectileID } = Terraria.ID;
const SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
const send = (...args) => Terraria.NetMessage[SEND](...args);

let fails = 0;
const side = () => (Main.netMode & 2) ? 'anfitrião' : 'cliente';
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpess ' + side() + ' ' + label + ': ok');
        else { fails++; bl.log('mpess ' + side() + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpess ' + side() + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const finish = () => bl.log('mpess ' + side() + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));

const seen = { orb: 0, fullOrb: 0 };

class MpGem extends ModItem {
    Texture = 'Gem';
    SetDefaults() {
        this.Item.width = this.Item.height = 20;
        this.Item.maxStack = 1;
    }
}
export class MpUntouchable extends MpGem { CanPickup(worldItem, player) { return false; } }
export class MpOrb extends MpGem {
    OnPickup(worldItem, player) {
        seen.orb++;
        bl.log(`mpess diag cliente OnPickup do orbe: espaço ${worldItem.whoAmI}, reservado para ${worldItem.playerIndexTheItemIsReservedFor}, quadro ${frames}`);
        return false;
    }
}
// Controle sem método nenhum (o cliente não tem outro igual para empilhar).
export class MpPlain extends MpGem {}
export class MpFullOrb extends MpGem {
    ItemSpace(item, player) { return true; }
    OnPickup(worldItem, player) { seen.fullOrb++; return false; }
}
export class MpMagnet extends MpGem { GrabRange(worldItem, player, grabRange) { grabRange.value += 400; } }

export class MpManyHook extends ModProjectile {
    Texture = 'Gem';
    SetDefaults() {
        this.CloneDefaults(ProjectileID.GemHookAmethyst);
        this.Projectile.width = this.Projectile.height = 18;
    }
    NumGrappleHooks(proj, player, numHooks) { numHooks.value = 5; }
}

export class MpFrozen extends ModProjectile {
    Texture = 'Gem';
    SetDefaults() {
        const p = this.Projectile;
        p.width = p.height = 10;
        p.aiStyle = 0;
        p.tileCollide = false;
        p.timeLeft = 900;
        p.friendly = true;
    }
    ShouldUpdatePosition(proj) { return false; }
}
export class MpMover extends MpFrozen { ShouldUpdatePosition(proj) { return true; } }

const I = (cls) => ModContent.ItemType(cls);
const P = (cls) => ModContent.ProjectileType(cls);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const killLocal = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const me = () => Main.player[Main.myPlayer];
const newItem = Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'];
const newProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

const area = () => ({ x: Main.spawnTileX + 100, floor: Main.spawnTileY - 40, ceiling: Main.spawnTileY - 52 });
const spot = () => { const { x, floor } = area(); return { X: x * 16, Y: (floor - 3) * 16 }; };

function count(player, type) {
    const inv = player.inventory;
    let n = 0;
    for (let k = 0; k < 58; k++) if (inv[k].type === type) n += inv[k].stack;
    return n;
}

// Quantos de um tipo no cofre do vazio (bank4) do jogador local.
function vault(type) {
    const items = me().bank4.item;
    let n = 0;
    for (let k = 0; k < items.length; k++) if (items[k].type === type) n += items[k].stack;
    return n;
}

// Os itens ativos de um tipo no chão (índices).
function onGround(type) {
    const out = [];
    for (let k = 0; k < 400; k++) {
        const w = Main.item[k];
        if (w.active && w.type === type) out.push(k);
    }
    return out;
}

// ---- anfitrião ----
const host = { still: 0, p1: 0, p2: 0, ids: {}, peak: 0, sixth: 0, hooksChecked: false, frozen: null, positionChecked: false, done: false };

function hostBuild() {
    const { x, floor, ceiling } = area();
    for (let i = x - 30; i <= x + 30; i++) {
        for (let j = ceiling; j <= floor; j++) if (bl.tiles.typeAt(i, j) >= 0) killLocal(i, j);
        placeTile(i, floor, TileID.Stone);
        placeTile(i, ceiling, TileID.Stone);
    }
}

function remoteClient() {
    for (let k = 0; k < 255; k++) if (k !== Main.myPlayer && Main.player[k].active) return Main.player[k];
    return null;
}

// Um item parado, com o centro dx pixels à direita do centro do jogador, mandado aos clientes.
function dropAt(player, type, dx) {
    const at = Vector2.new(player.Center.X + dx, player.position.Y + player.height - 12);
    const i = newItem(null, at, type, 1, 0, 0, null, null, false);
    Main.item[i].velocity = Vector2.new(0, 0);
    send(21, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
    return i;
}

// O item do espaço i ainda é o mesmo (o jogo reusa o espaço de um item que saiu).
const alive = (i, type) => Main.item[i].active && Main.item[i].type === type;

function removeItem(i, type) {
    if (i >= 0 && alive(i, type)) {
        Main.item[i]['void TurnToAir()']();
        send(21, -1, -1, null, i, 0, 0, 0, 0, 0, 0);
    }
}

function hostWatch(frames) {
    const other = remoteClient();
    if (!other) return;
    const s = spot();
    const atSpot = Math.abs(other.position.X - s.X) < 48 && Math.abs(other.position.Y - s.Y) < 48;
    host.still = atSpot ? host.still + 1 : 0;

    // Coleta, 1ª parte.
    if (!host.p1 && host.still >= 90) {
        host.p1 = frames;
        host.ids.orb = dropAt(other, I(MpOrb), 0);
        host.ids.untouchable = dropAt(other, I(MpUntouchable), 0);
        host.ids.magnet = dropAt(other, I(MpMagnet), -20 * 16);
        host.ids.wood = dropAt(other, ItemID.Wood, 12 * 16);
        bl.log('mpess anfitrião itens soltos junto do jogador ' + other.whoAmI);
    }
    if (host.p1 && frames - host.p1 <= 120 && (frames - host.p1) % 10 === 0) {
        const o = Main.item[host.ids.orb];
        bl.log(`mpess diag anfitrião orbe +${frames - host.p1}: ativo ${o.active}, espaço ${host.ids.orb}, reservado para ${o.playerIndexTheItemIsReservedFor}`);
    }
    if (host.p1 && frames === host.p1 + 360) {
        const ids = host.ids;
        check('OnPickup false no cliente: o orbe sumiu do servidor', () => !alive(ids.orb, I(MpOrb)) || 'ainda no chão');
        check('GrabRange +400: o ímã foi puxado e pego pelo cliente', () => (!alive(ids.magnet, I(MpMagnet)) && count(other, I(MpMagnet)) === 1) || `no chão ${alive(ids.magnet, I(MpMagnet))}, no inventário ${count(other, I(MpMagnet))}`);
        check('CanPickup false: continua no chão do servidor', () => alive(ids.untouchable, I(MpUntouchable)) || 'sumiu');
        check('a madeira a 12 blocos continua no chão', () => alive(ids.wood, ItemID.Wood) || 'sumiu');
        removeItem(host.ids.untouchable, I(MpUntouchable));
        removeItem(host.ids.wood, ItemID.Wood);
    }

    // Coleta, 2ª parte: o inventário do cliente cheio, visto pelo servidor.
    if (host.p1 && !host.p2 && frames > host.p1 + 360) {
        let full = true;
        for (let k = 0; k < 50; k++) if (other.inventory[k].type === 0) { full = false; break; }
        if (full) {
            host.p2 = frames;
            host.ids.fullOrb = dropAt(other, I(MpFullOrb), 0);
            host.ids.plain = dropAt(other, I(MpPlain), 0);
            // Com o Saco do Vazio o jogo põe no cofre do vazio quando o
            // inventário enche (ItemSpace diz que cabe): o esperado segue o jogo.
            host.plainFits = other['Player.ItemSpaceStatus ItemSpace(Item newItem)'](Terraria.ID.ContentSamples.ItemsByType.get_Item(I(MpPlain))).CanTakeItem;
            bl.log('mpess anfitrião com o inventário cheio, o jogo diz que o item comum ' + (host.plainFits ? 'cabe (cofre do vazio)' : 'não cabe'));
        }
    }
    if (host.p2 && frames === host.p2 + 300) {
        check('ItemSpace true: com o inventário cheio, o orbe foi pego', () => !alive(host.ids.fullOrb, I(MpFullOrb)) || 'ainda no chão');
        // Onde o item comum está, se não no espaço dele.
        const ground = onGround(I(MpPlain)), held = count(other, I(MpPlain));
        bl.log(`mpess diag anfitrião item comum: no espaço ${alive(host.ids.plain, I(MpPlain))}, no chão em ${JSON.stringify(ground)}, no inventário do cliente ${held}`);
        if (host.plainFits) check('sem ItemSpace, o item comum vai para o cofre do vazio do cliente', () => !alive(host.ids.plain, I(MpPlain)) || 'ficou no chão');
        else check('sem ItemSpace, o item comum não foi reservado ao cliente cheio', () => onGround(I(MpPlain)).length > 0 || 'sumiu');
        for (const k of onGround(I(MpPlain))) removeItem(k, I(MpPlain));
    }

    // Ganchos do cliente: com o sexto voando, o servidor chega a ver 6; depois, 5.
    if (!host.hooksChecked && frames % 4 === 0) {   // 1000 projéteis pela ponte: não todo quadro
        let active = 0;
        for (let k = 0; k < 1000; k++) {
            const q = Main.projectile[k];
            if (q.active && q.owner === other.whoAmI && q.type === P(MpManyHook)) active++;
        }
        host.peak = Math.max(host.peak || 0, active);
        if (host.peak >= 6 && !host.sixth) host.sixth = frames;
        if (host.sixth && frames >= host.sixth + 120) {
            host.hooksChecked = true;
            check('NumGrappleHooks 5: o servidor fica com 5 ganchos do cliente', () => active === 5 || `${active} ativos (pico ${host.peak})`);
        }
    }

    // ShouldUpdatePosition na cópia do servidor.
    if (!host.frozen) {
        if (frames % 4 !== 0) return;
        let frozen = null, mover = null;
        for (let k = 0; k < 1000; k++) {
            const q = Main.projectile[k];
            if (!q.active || q.owner !== other.whoAmI) continue;
            if (q.type === P(MpFrozen)) frozen = q;
            if (q.type === P(MpMover)) mover = q;
        }
        if (frozen && mover) host.frozen = { frozen, mover, x: frozen.position.X, mx: mover.position.X, at: frames };
    } else if (!host.positionChecked && frames >= host.frozen.at + 40) {
        host.positionChecked = true;
        const { frozen, mover, x, mx } = host.frozen;
        check('ShouldUpdatePosition false: a cópia do servidor não anda', () =>
            (Math.abs(frozen.position.X - x) < 1 && frozen.velocity.X === 5) || `x ${x} -> ${frozen.position.X}, vx ${frozen.velocity.X}`);
        check('ShouldUpdatePosition true: a de controle anda no servidor', () => mover.position.X - mx > 100 || `x ${mx} -> ${mover.position.X}`);
    }

    const timeout = frames > 60 * 240;
    if ((host.positionChecked && host.hooksChecked && host.p2) || timeout) {
        if (timeout) check('o cliente fez todas as etapas', () => `p1 ${!!host.p1}, p2 ${!!host.p2}, ganchos ${host.hooksChecked}, posição ${host.positionChecked}`);
        host.done = true;
        const { x, floor, ceiling } = area();
        for (let i = x - 30; i <= x + 30; i++) for (const j of [floor, ceiling]) if (bl.tiles.typeAt(i, j) >= 0) {
            killLocal(i, j);
            send(17, -1, -1, null, 0, i, j, 0, 0, 0, 0);
        }
        finish();
    }
}

// ---- cliente ----
const client = { step: 0, wait: 0, filled: [], hooks: [], done: false };
function next() { client.step++; client.wait = 0; }

function stand() {
    const p = me(), s = spot();
    p.position = Vector2.new(s.X, s.Y);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = Math.floor(s.Y / 16);
    if (client.wait % 10 === 0) send(13, -1, -1, null, Main.myPlayer, 0, 0, 0, 0, 0, 0);
}

function syncSlot(k) { send(5, -1, -1, null, Main.myPlayer, k, 0, 0, 0, 0, 0); }

function fire(type, vx, vy, dy = 0) {
    const c = me().Center;
    return Main.projectile[newProjectile(null, c.X, c.Y + dy, vx, vy, type, 0, 0, Main.myPlayer, 0, 0, 0, null)];
}

function clientStep() {
    client.wait++;
    if (client.step <= 3) stand();
    switch (client.step) {
        case 0: {
            const done = seen.orb > 0 && count(me(), I(MpMagnet)) > 0;
            if (!done && client.wait < 1200) return;
            check('OnPickup false: chamado no cliente, o orbe não entra no inventário', () =>
                (seen.orb === 1 && count(me(), I(MpOrb)) === 0) || `chamadas ${seen.orb}, no inventário ${count(me(), I(MpOrb))}`);
            check('GrabRange +400: o ímã de 20 blocos veio para o inventário', () => count(me(), I(MpMagnet)) === 1 || 'no inventário ' + count(me(), I(MpMagnet)));
            check('CanPickup false: o item do servidor ficou no chão', () =>
                (onGround(I(MpUntouchable)).length > 0 && count(me(), I(MpUntouchable)) === 0) || `no chão ${onGround(I(MpUntouchable)).length}, no inventário ${count(me(), I(MpUntouchable))}`);
            return next();
        }
        case 1: {
            if (client.wait < 120) return;   // o servidor confere e tira o que ficou
            const inv = me().inventory;
            // Os itens comuns que rodadas anteriores deixaram (o teste começa sem nenhum).
            for (let k = 0; k < 58; k++) if (inv[k].type === I(MpPlain)) { inv[k]['void TurnToAir()'](); syncSlot(k); }
            for (let k = 0; k < 50; k++) {
                if (inv[k].type !== 0) continue;
                inv[k]['void SetDefaults(int Type, ItemVariant variant)'](ItemID.DirtBlock, null);
                client.filled.push(k);
                syncSlot(k);
            }
            client.plain = count(me(), I(MpPlain));
            client.plainFits = me()['Player.ItemSpaceStatus ItemSpace(Item newItem)'](Terraria.ID.ContentSamples.ItemsByType.get_Item(I(MpPlain))).CanTakeItem;
            bl.log('mpess cliente inventário cheio (' + client.filled.length + ' casas)');
            return next();
        }
        case 2: {
            // O inventário fica cheio até o servidor conferir o item comum: ele o
            // tira do chão logo depois (com espaço, o item seria reservado e pego).
            if (client.wait % 5 !== 0 && client.wait < 1800) return;   // 400 itens pela ponte: não todo quadro
            if (onGround(I(MpPlain)).length > 0) client.sawPlain = true;
            const checked = seen.fullOrb > 0 && client.sawPlain && onGround(I(MpPlain)).length === 0;
            if (!checked && client.wait < 1800) return;
            check('ItemSpace true: com o inventário cheio, pega e consome', () => seen.fullOrb === 1 || 'chamadas ' + seen.fullOrb);
            if (client.plainFits) check('sem ItemSpace, o item comum vai para o cofre do vazio', () => vault(I(MpPlain)) > 0 || 'não está no cofre');
            else check('sem ItemSpace, o item comum não entra', () => count(me(), I(MpPlain)) === client.plain || `${client.plain} -> ${count(me(), I(MpPlain))}`);
            const inv = me().inventory;
            for (const k of client.filled) if (inv[k].type === ItemID.DirtBlock) { inv[k]['void TurnToAir()'](); syncSlot(k); }
            client.filled = [];
            return next();
        }
        case 3: {
            // Seis ganchos no teto, um a cada 8 quadros.
            if (client.wait <= 48 && (client.wait - 1) % 8 === 0) client.hooks.push(fire(P(MpManyHook), ((client.wait - 1) / 8 - 2.5) * 2, -14));
            if (client.wait < 120) return;
            const latched = bl.projectiles.latchedHooks(Main.myPlayer, -1).filter((h) => h.type === P(MpManyHook));
            check('NumGrappleHooks 5: cinco presos, o mais velho soltou', () =>
                (latched.length === 5 && !client.hooks[0].active) || `${latched.length} presos, o primeiro ${client.hooks[0].active ? 'ativo' : 'solto'}`);
            return next();
        }
        case 4: {
            if (client.wait < 180) return;   // o servidor conta os dele
            for (const h of client.hooks) if (h.active) h['void Kill()']();
            client.frozen = fire(P(MpFrozen), 5, 0, -40);
            client.mover = fire(P(MpMover), 5, 0, -60);
            client.x = client.frozen.position.X;
            client.mx = client.mover.position.X;
            return next();
        }
        case 5: {
            if (client.wait < 40) return;
            check('ShouldUpdatePosition false: o projétil do cliente não anda', () =>
                Math.abs(client.frozen.position.X - client.x) < 1 || `x ${client.x} -> ${client.frozen.position.X}`);
            check('ShouldUpdatePosition true: o de controle anda', () => client.mover.position.X - client.mx > 100 || `x ${client.mx} -> ${client.mover.position.X}`);
            return next();
        }
        case 6: {
            if (client.wait < 180) return;   // o servidor confere as cópias dele
            client.frozen['void Kill()']();
            client.mover['void Kill()']();
            const inv = me().inventory;
            for (let k = 0; k < 58; k++) if (inv[k].type === I(MpMagnet)) { inv[k]['void TurnToAir()'](); syncSlot(k); }
            const bank = me().bank4.item;
            for (let k = 0; k < bank.length; k++) if (bank[k].type === I(MpPlain)) bank[k]['void TurnToAir()']();
            const p = me();
            p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
            p.fallStart = Main.spawnTileY - 3;
            client.done = true;
            finish();
            return;
        }
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || Main.netMode === 0) return;
    frames++;
    self.statLife = self.statLifeMax2;
    if (Main.netMode & 2) {
        if (frames === 60) check('preparo', hostBuild);
        else if (frames > 60 && !host.done) hostWatch(frames);
    } else if (frames >= 300 && !client.done) {
        clientStep();
    }
});
bl.log('mpess: carregado');

export default class TestMpEssentials extends Mod {}
