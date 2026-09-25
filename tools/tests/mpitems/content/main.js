// Itens do Example Mod no multijogador. O cliente usa de verdade (o
// controlUseItem, como o dedo) cada item de mod que atira e bebe a pocao; o
// host confere o que chegou pela rede: o item na mao do cliente com o ModItem,
// o projetil do cliente com o ModProjectile, e o buff de mod no cliente.
// Depois, o dash do escudo: os dois lados logam cada dash que veem, para o
// teste com toque de verdade no cliente. Loga "mpi <papel> <caso>: ok | FALHOU".
const Main = Terraria.Main;
const VANILLA_ITEMS = bl.items.vanillaCount;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpi ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpi ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpi ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpi ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const itemName = (t) => Terraria.Lang['string GetItemNameValue(int id)'](t);
const className = (t) => { const m = ModItem.getModItem(t); return m ? m.constructor.name : itemName(t); };
const heldOf = (p) => p.inventory[p.selectedItemState.selected];
const scratch = Terraria.Item.new();
scratch['void .ctor()']();
function defaultsOf(t) {
    scratch['void SetDefaults(int Type, ItemVariant variant)'](t, null);
    return { shoot: scratch.shoot, useAmmo: scratch.useAmmo, ammo: scratch.ammo, useStyle: scratch.useStyle,
             buffType: scratch.buffType, consumable: scratch.consumable };
}

// O tiro de arma de fogo vem da MUNICAO: o `shoot` do item e so o padrao.
function shootsOf(t) {
    const d = defaultsOf(t);
    const out = [d.shoot];
    if (d.useAmmo > 0) out.push(defaultsOf(ammoFor(d.useAmmo)).shoot);
    return out;
}
function modItems() {
    const out = [];
    for (let t = VANILLA_ITEMS; t < VANILLA_ITEMS + 400 && bl.items.isModItem(t); t++) out.push(t);
    return out;
}
function ownedProjectiles(owner, types) {
    let n = 0, bound = 0;
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.owner === owner && types.includes(pr.type)) {
            n++;
            if (pr.ModProjectile || !bl.projectiles.isModProjectile(pr.type)) bound++;
        }
    }
    return { n, bound };
}

function killOwned(owner) {
    let n = 0;
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        // Sem Kill(): o foguete explodiria em cima do jogador.
        if (pr.active && pr.owner === owner) {
            pr.active = false;
            sendData(29, -1, -1, null, pr.identity, owner, 0, 0, 0, 0, 0);
            n++;
        }
    }
    return n;
}

// ------------------------------- cliente -------------------------------
const plan = [];
let step = -1, stepFrame = 0, forceUse = false, saved = null;
const localSeen = new Map();

function clientPlan() {
    for (const t of modItems()) {
        const d = defaultsOf(t);
        const isHook = d.shoot > 0 && Main.projHook[d.shoot];
        if (d.buffType > 0 && d.consumable) plan.push({ t, kind: 'pocao', buff: d.buffType });
        else if (d.shoot > 0 && !isHook && d.useStyle > 0) plan.push({ t, kind: 'tiro', shoots: shootsOf(t), ammo: d.useAmmo });
    }
    // Controle: o ioio do jogo, pelo mesmo caminho.
    plan.push({ t: 3278, kind: 'tiro', shoots: shootsOf(3278), ammo: 0 });
    bl.log('mpi cliente: plano ' + plan.map((s) => className(s.t) + (s.kind === 'pocao' ? '(pocao)' : '')).join(', '));
}
function setSlot(p, slot, type, stack) {
    p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (type > 0) p.inventory[slot].stack = stack;
    sendData(5, -1, -1, null, p.whoAmI, slot, p.inventory[slot].prefix, 0, 0, 0, 0);
}
function ammoFor(useAmmo) {
    if (useAmmo <= 0) return 0;
    for (const t of modItems()) if (defaultsOf(t).ammo === useAmmo) return t;
    return useAmmo;
}
function clientStart() {
    const p = Main.player[Main.myPlayer];
    saved = { sel: p.selectedItemState.selected, slot0: p.inventory[0].type, s0: p.inventory[0].stack,
              ammo: p.inventory[54].type, a: p.inventory[54].stack };
    clientPlan();
    step = 0;
    stepFrame = 0;
}
function clientStep() {
    const p = Main.player[Main.myPlayer];
    const s = plan[step];
    if (stepFrame === 0) {
        // Passo independente: nada do item anterior ainda no ar, nem animacao.
        if (p.itemAnimation > 0 || killOwned(p.whoAmI) > 0) return;
        setSlot(p, 0, s.t, s.kind === 'pocao' ? 5 : 1);
        if (s.kind === 'tiro' && s.ammo > 0) setSlot(p, 54, ammoFor(s.ammo), 99);
        p.selectedItemState.selected = 0;
    }
    if (stepFrame === 10) forceUse = true;
    if (s.kind === 'tiro' && forceUse) {
        const got = ownedProjectiles(Main.myPlayer, s.shoots);
        if (got.n > 0) localSeen.set(s.t, Math.max(localSeen.get(s.t) || 0, got.bound > 0 ? 2 : 1));
    }
    if (stepFrame === 50) {
        forceUse = false;
        const n = className(s.t);
        if (s.kind === 'pocao') {
            check(n + ': bebida, com o buff', () => {
                const has = p['int FindBuffIndex(int type)'](s.buff) >= 0;
                return (has && p.inventory[0].stack === 4) || `buff ${has}, pilha ${p.inventory[0].stack}`;
            });
        } else {
            check(n + ': projetil de mod saiu, com ModProjectile', () =>
                localSeen.get(s.t) === 2 || (localSeen.has(s.t) ? 'sem ModProjectile' : 'nenhum projetil ' + s.shoots.join('/')));
        }
    }
    if (++stepFrame >= 70) {
        stepFrame = 0;
        if (++step >= plan.length) clientEnd();
    }
}
function clientEnd() {
    const p = Main.player[Main.myPlayer];
    setSlot(p, 0, saved.slot0, saved.s0);
    setSlot(p, 54, saved.ammo, saved.a);
    p.selectedItemState.selected = saved.sel;
    step = -1;
    // O escudo fica equipado para o teste do dash com toque de verdade.
    const shield = modItems().find((t) => className(t) === 'ExampleShield');
    p.armor[3]['void SetDefaults(int Type, ItemVariant variant)'](shield, null);
    sendData(5, -1, -1, null, p.whoAmI, Terraria.ID.PlayerItemSlotID.Armor0 + 3, 0, 0, 0, 0, 0);
    bl.log('mpi cliente: itens terminados; escudo equipado, agora o dash com toque de verdade');
    finish();
}

Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

// -------------------------------- host --------------------------------
const hostHeld = new Map();
const remoteProj = new Map();   // tipo de projetil de mod do cliente -> ja veio com ModProjectile
let hostStarted = -1, lastModHeld = -1, sawBuff = false;
// A bala que nasce e morre no mesmo pacote nunca aparece ativa num quadro do
// host: conta tambem na criacao. O host fica parado no teste, entao projetil
// de mod criado aqui e do cliente.
Terraria.Projectile['void SetDefaults(int Type)'].hook((original, self, type) => {
    original(self, type);
    // O ModProjectile e ligado por fora deste hook (o Example Mod carrega
    // depois): confere no proximo quadro, mesmo que ele ja tenha morrido.
    if (role === 'host' && !done && bl.projectiles.isModProjectile(type)) created.push({ proj: self, type });
});
const created = [];
function settleCreated() {
    for (const c of created.splice(0)) {
        if (c.proj.type === c.type) remoteProj.set(c.type, (remoteProj.get(c.type) || false) || !!c.proj.ModProjectile);
    }
}
function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}
function hostPoll(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    const remote = Main.player[r];
    settleCreated();
    if (hostStarted < 0) { hostStarted = frames; bl.log('mpi host: cliente entrou (' + remote.name + ')'); }
    const held = heldOf(remote);
    if (held && bl.items.isModItem(held.type)) {
        lastModHeld = frames;
        const e = hostHeld.get(held.type) || { bound: false, shoot: defaultsOf(held.type).shoot, shoots: shootsOf(held.type), proj: 0, projBound: 0 };
        if (held.ModItem) e.bound = true;
        if (e.shoot > 0) {
            const got = ownedProjectiles(r, e.shoots);
            e.proj = Math.max(e.proj, got.n);
            e.projBound = Math.max(e.projBound, got.bound);
        }
        hostHeld.set(held.type, e);
    }
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.owner === r && bl.projectiles.isModProjectile(pr.type)) {
            remoteProj.set(pr.type, (remoteProj.get(pr.type) || false) || !!pr.ModProjectile);
        }
    }
    for (let b = 0; b < remote.buffType.length; b++) {
        if (bl.buffs.isModBuff(remote.buffType[b])) sawBuff = true;
    }
}
function hostReport() {
    for (const [t, e] of hostHeld) {
        const n = className(t);
        check(n + ': na mao do cliente, com ModItem', () => e.bound || 'sem ModItem');
        if (e.shoot > 0 && !Main.projHook[e.shoot]) {
            check(n + ': projetil do cliente chegou, com ModProjectile', () => {
                const mod = e.shoots.filter((t) => bl.projectiles.isModProjectile(t));
                if (mod.length) return mod.some((t) => remoteProj.get(t) === true) ||
                    `tipos ${mod.join('/')}: ${mod.map((t) => remoteProj.has(t) ? 'sem instancia' : 'nao vistos').join('/')}`;
                return (e.proj > 0 && e.projBound > 0) || `vistos ${e.proj}`;
            });
        }
    }
    check('buff de mod do cliente chegou ao host', () => sawBuff || 'nenhum buff de mod no cliente');
    const me = Main.player[Main.myPlayer];
    const shield = modItems().find((t) => className(t) === 'ExampleShield');
    me.armor[3]['void SetDefaults(int Type, ItemVariant variant)'](shield, null);
    sendData(5, -1, -1, null, me.whoAmI, Terraria.ID.PlayerItemSlotID.Armor0 + 3, 0, 0, 0, 0, 0);
    bl.log('mpi host: itens terminados; escudo equipado, agora o dash com toque de verdade');
    finish();
}

// ------------------------- dash (os dois lados) -------------------------
const dashing = new Map();
function watchDash() {
    for (let i = 0; i < 255; i++) {
        const p = Main.player[i];
        if (!p.active) continue;
        const d = p.GetModPlayer('ExampleDashPlayer');
        const on = !!d && d.DashTimer > 0;
        if (on && !dashing.get(i)) {
            const who = i === Main.myPlayer ? 'eu' : 'o outro (' + p.name + ')';
            bl.log(`mpi ${role} dash: ${who} vel ${p.velocity.X.toFixed(1)},${p.velocity.Y.toFixed(1)} escudo ${d.DashAccessoryEquipped}`);
        }
        dashing.set(i, on);
        // O dash de outro jogador chega pela posicao: ~10 px por quadro, bem
        // mais que andando (~3 px, 36 em 12 quadros).
        if (i !== Main.myPlayer) {
            const xs = trail.get(i) || [];
            xs.push(p.position.X);
            if (xs.length > 12) xs.shift();
            trail.set(i, xs);
            const moved = xs[xs.length - 1] - xs[0];
            if (xs.length === 12 && Math.abs(moved) > 60 && frames - (lastRemoteDash.get(i) || -99) > 40) {
                lastRemoteDash.set(i, frames);
                bl.log(`mpi ${role} dash do outro (${p.name}) visto pela posicao: ${moved.toFixed(0)} px em 12 quadros`);
            }
        }
    }
}
const trail = new Map(), lastRemoteDash = new Map();

let frames = 0, deathLogged = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mpi: netMode ${mode}, papel ${role || 'nenhum'}`);
    }
    if (!role) return;
    if (!self.dead) { self.statLife = self.statLifeMax2; self.statMana = self.statManaMax2; self.immune = true; self.immuneTime = 10; }
    if (self.dead && !deathLogged) { deathLogged = true; bl.log('mpi ' + role + ': MORREU no quadro ' + frames); }
    watchDash();
    if (done) return;
    if (role === 'cliente') {
        if (frames === 150) clientStart();
        else if (step >= 0) clientStep();
    } else {
        hostPoll(frames);
        if (hostHeld.size > 0 && frames - lastModHeld > 200) hostReport();
        else if (hostStarted >= 0 && frames - hostStarted > 4000 && !hostHeld.size) {
            check('o host viu algum item de mod na mao do cliente', () => 'nenhum');
            finish();
        }
    }
});
bl.log('mpi: carregado');
