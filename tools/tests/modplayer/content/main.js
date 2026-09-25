// ModPlayer: uma instancia por jogador, ordem dos ganchos no quadro, dano,
// morte e renascer, e o conteudo da etapa 4 do Example Mod (dash do escudo,
// debuff de defesa, acessorios, vaidade). Precisa do Example Mod ligado.
// Loga "modplayer <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;

const flags = { captureNext: false, capture: false, bonusLife: 0, doubleDamageOf: -1, blockUseOf: -1, immune: false, dodge: false,
                hurtTo: -1, blockKill: false, forceDash: false };
const events = [];
const spy = { enter: 0, respawn: 0, kill: 0, onHurt: [], postHurt: 0, dashVelocity: null, resetOf: new Map() };

class TestPlayer extends ModPlayer {
    initCount = 0;
    note = '';
    runs = 0;
    loaded = null;
    SaveData(data) {
        data.runs = this.runs;
        data.nested = { ok: true, list: [1, 2, 3] };
    }
    LoadData(data) {
        this.loaded = data;
        this.runs = data.runs;
    }
    Initialize() { this.initCount++; }
    OnEnterWorld(player) { if (player.whoAmI === Main.myPlayer) spy.enter++; }
    OnRespawn(player) { spy.respawn++; }
    rec(name, player) { if (flags.capture && player.whoAmI === Main.myPlayer) events.push(name); }
    PreUpdate(player) {
        if (flags.captureNext && player.whoAmI === Main.myPlayer) { flags.captureNext = false; flags.capture = true; }
        this.rec('PreUpdate', player);
    }
    ResetEffects(player) {
        this.rec('ResetEffects', player);
        spy.resetOf.set(player.whoAmI, (spy.resetOf.get(player.whoAmI) || 0) + 1);
    }
    ModifyMaxStats(player) {
        super.ModifyMaxStats(player);
        if (player.whoAmI === Main.myPlayer) this.CumulativeHealth = flags.bonusLife;
    }
    PreUpdateBuffs(player) { this.rec('PreUpdateBuffs', player); }
    PostUpdateBuffs(player) { this.rec('PostUpdateBuffs', player); }
    UpdateEquips(player) {
        this.rec('UpdateEquips', player);
        if (flags.forceDash && player.whoAmI === Main.myPlayer) {
            flags.forceDash = false;
            player.GetModPlayer('ExampleDashPlayer').DashDir = 2;
            spy.dashArmed = true;
        }
    }
    UpdateBadLifeRegen(player) { this.rec('UpdateBadLifeRegen', player); }
    UpdateLifeRegen(player) { this.rec('UpdateLifeRegen', player); }
    UpdateManaRegen(player) { this.rec('UpdateManaRegen', player); }
    UpdateMovement(player) {
        this.rec('UpdateMovement', player);
        if (spy.dashArmed && player.whoAmI === Main.myPlayer) {
            spy.dashArmed = false;
            spy.dashVelocity = player.velocity.X;
        }
    }
    PostUpdate(player) {
        this.rec('PostUpdate', player);
        if (player.whoAmI === Main.myPlayer) flags.capture = false;
    }
    CanUseItem(player, item) { return item.type !== flags.blockUseOf; }
    ModifyWeaponDamage(player, item, damage) {
        if (item.type === flags.doubleDamageOf) return damage * 2;
    }
    ImmuneTo() { return flags.immune; }
    FreeDodge() { return flags.dodge; }
    ModifyHurt(player, mod) { if (flags.hurtTo >= 0) mod.damage = flags.hurtTo; }
    OnHurt(player, src, damage) { spy.onHurt.push(damage); }
    PostHurt() { spy.postHurt++; }
    PreKill() { return !flags.blockKill; }
    Kill() { spy.kill++; }
}

class OtherPlayer extends ModPlayer {
    value = 1;
}

ModPlayer.register(TestPlayer);
ModPlayer.register(OtherPlayer);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modplayer ' + label + ': ok');
        else { fails++; bl.log('modplayer ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modplayer ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];
const reason = () => Terraria.DataStructures.PlayerDeathReason['PlayerDeathReason LegacyDefault()']();
const hurt = (p, dmg) => {
    p.immune = false;
    p.immuneTime = 0;
    return p['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'](
        reason(), dmg, 0, false, true, false, -1, false);
};
const sample = (type) => {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
};
const setItem = (it, type) => it['void SetDefaults(int Type, ItemVariant variant)'](type, null);

function modType(...names) {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 400 && bl.items.isModItem(t); t++) {
        if (names.includes(Terraria.Lang['string GetItemNameValue(int id)'](t))) return t;
    }
    return -1;
}
function modBuff(...names) {
    for (let t = bl.buffs.vanillaCount; t < bl.buffs.vanillaCount + 64 && bl.buffs.isModBuff(t); t++) {
        if (names.includes(Terraria.Lang['string GetBuffName(int id)'](t))) return t;
    }
    return -1;
}

// Slots de acessorio emprestados: [indice em armor, tipo de antes].
const borrowed = [];
function equip(p, slot, type) {
    borrowed.push([p, slot, p.armor[slot].type, p.armor[slot].stack]);
    setItem(p.armor[slot], type);
}
function restoreSlots() {
    while (borrowed.length) {
        const [p, slot, type, stack] = borrowed.pop();
        setItem(p.armor[slot], type);
        p.armor[slot].stack = stack;
    }
}

const snap = {};
let SHIELD = -1, STAT = -1, BOOTS = -1, NEST = -1, DEBUFF = -1, p2 = null;

function identity() {
    const p = me();
    check('instancia do jogador local', () => {
        const a = p.GetModPlayer(TestPlayer);
        if (!(a instanceof TestPlayer)) return 'GetModPlayer ' + a;
        if (a !== TestPlayer.get(p) || a !== p.GetModPlayer('TestPlayer')) return 'get/nome diferentes';
        if (a !== ModPlayer.getByName('TestPlayer')) return 'getByName';
        if (a.Player !== p) return 'this.Player';
        return a.initCount === 1 || 'Initialize ' + a.initCount;
    });
    check('cada jogador tem a sua', () => {
        const other = Main.player[1];
        const a = p.GetModPlayer(TestPlayer), b = other.GetModPlayer(TestPlayer);
        if (!b || a === b) return 'mesma instancia';
        a.note = 'local';
        if (b.note !== '') return 'estado vazou';
        if (b.Player !== other || b.initCount !== 1) return 'b.Player ' + (b.Player === other) + ', init ' + b.initCount;
        const o = p.GetModPlayer(OtherPlayer);
        return (o instanceof OtherPlayer && o.value === 1 && o !== other.GetModPlayer(OtherPlayer)) || 'OtherPlayer';
    });
    check('OnEnterWorld', () => spy.enter === 1 || 'enter ' + spy.enter);
    const t = p.GetModPlayer(TestPlayer);
    if (!t.loaded) bl.log('modplayer save: nada salvo ainda (primeira rodada?)');
    else check('LoadData: o que a rodada anterior salvou', () =>
        (t.runs >= 1 && t.loaded.nested && t.loaded.nested.ok === true && t.loaded.nested.list.length === 3) ||
        JSON.stringify(t.loaded));
    t.runs++;
}

function saveCheck() {
    const fd = Main.ActivePlayerFileData;
    const file = fd.Path + '.bl.json';
    const before = JSON.parse(bl.file.read(file) || '{}');
    before['outro-mod/Fantasma'] = { x: 1 };
    bl.file.write(file, JSON.stringify(before));
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](fd, false, true);
    check('SaveData grava e mantem dados de mod ausente', () => {
        const after = JSON.parse(bl.file.read(file) || '{}');
        const mine = after['a2906241-2c04-41f4-b662-7ba00e162fe6/TestPlayer'];
        if (!mine || mine.runs !== me().GetModPlayer(TestPlayer).runs || !mine.nested.ok) return 'TestPlayer ' + JSON.stringify(mine);
        if (after['a2906241-2c04-41f4-b662-7ba00e162fe6/OtherPlayer']) return 'OtherPlayer sem SaveData gravou';
        return (after['outro-mod/Fantasma'] && after['outro-mod/Fantasma'].x === 1) || 'a chave do mod ausente sumiu';
    });
}

function orderCheck() {
    check('ordem dos ganchos num quadro', () => {
        const want = ['PreUpdate', 'ResetEffects', 'PreUpdateBuffs', 'PostUpdateBuffs', 'UpdateEquips',
                      'UpdateBadLifeRegen', 'UpdateLifeRegen', 'UpdateManaRegen', 'UpdateMovement', 'PostUpdate'];
        return JSON.stringify(events) === JSON.stringify(want) || events.join(',');
    });
}

function numbersChecks() {
    const p = me();
    check('ModifyMaxStats: +50 de vida maxima', () =>
        p.statLifeMax2 === snap.lifeMax + 50 || `antes ${snap.lifeMax}, agora ${p.statLifeMax2}`);
    flags.bonusLife = 0;

    check('ModifyWeaponDamage', () => {
        const sword = sample(ItemID.IronBroadsword);
        const base = p['int GetWeaponDamage(Item sItem)'](sword);
        flags.doubleDamageOf = ItemID.IronBroadsword;
        const doubled = p['int GetWeaponDamage(Item sItem)'](sword);
        flags.doubleDamageOf = -1;
        return (base > 0 && doubled === base * 2) || `base ${base}, dobro ${doubled}`;
    });

    check('CanUseItem', () => {
        const sword = sample(ItemID.IronBroadsword);
        const canUse = (it) => p['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'](it, false);
        const before = canUse(sword);
        flags.blockUseOf = ItemID.IronBroadsword;
        const blocked = canUse(sword);
        flags.blockUseOf = -1;
        return (before === true && blocked === false) || `antes ${before}, bloqueado ${blocked}`;
    });
}

function hurtChecks() {
    const p = me();
    p.statLife = p.statLifeMax2;
    check('ImmuneTo', () => {
        flags.immune = true;
        const life = p.statLife, r = hurt(p, 50);
        flags.immune = false;
        return (r === 0 && p.statLife === life && spy.onHurt.length === 0) || `r ${r}, vida ${life}->${p.statLife}`;
    });
    check('FreeDodge', () => {
        flags.dodge = true;
        const life = p.statLife, r = hurt(p, 50);
        flags.dodge = false;
        return (r === 0 && p.statLife === life) || `r ${r}, vida ${life}->${p.statLife}`;
    });
    check('ModifyHurt, OnHurt, PostHurt', () => {
        // O grande nao pode matar (morto, o jogador nao processa equipamento
        // e os outros testes que rodam junto perdem os buffs).
        flags.hurtTo = 1;
        const small = hurt(p, 30);
        p.statLife = p.statLifeMax2;
        flags.hurtTo = 30;
        const big = hurt(p, 1);
        flags.hurtTo = -1;
        p.statLife = p.statLifeMax2;
        if (!(small > 0 && big > small)) return `pequeno ${small}, grande ${big}`;
        return (spy.onHurt.length === 2 && spy.onHurt[0] === small && spy.onHurt[1] === big && spy.postHurt === 2) ||
            `OnHurt ${JSON.stringify(spy.onHurt)}, PostHurt ${spy.postHurt}`;
    });
}

function equipExample() {
    const p = me();
    SHIELD = modType('Escudo de Exemplo', 'Example Shield');
    STAT = modType('Acessório de Estatísticas', 'Stat Accessory');
    BOOTS = modType('Botas de Exemplo', 'Example Boots');
    NEST = modType('Ninho de Vespas', 'Wasp Nest');
    DEBUFF = modBuff('Defesa Reduzida', 'Reduced Defense');
    snap.melee = p.meleeDamage;
    snap.move = p.moveSpeed;
    snap.noFall = p.noFallDmg;
    equip(p, 3, SHIELD);
    equip(p, 4, STAT);
    equip(p, 5, BOOTS);
    equip(p, 6, NEST);
}

function exampleChecks() {
    const p = me();
    check('Example Mod: itens e buff achados', () =>
        (SHIELD > 0 && STAT > 0 && BOOTS > 0 && NEST > 0 && DEBUFF > 0) || JSON.stringify({ SHIELD, STAT, BOOTS, NEST, DEBUFF }));
    check('escudo liga o dash (UpdateAccessory -> GetModPlayer)', () =>
        p.GetModPlayer('ExampleDashPlayer').DashAccessoryEquipped === true || 'DashAccessoryEquipped falso');
    check('acessorio de dano: +10%', () =>
        Math.abs(p.meleeDamage - snap.melee * 1.1) < 0.001 || `antes ${snap.melee}, agora ${p.meleeDamage}`);
    check('botas: velocidade e sem dano de queda', () =>
        (Math.abs(p.moveSpeed - (snap.move + 0.08)) < 0.001 && p.noFallDmg && p.hellfireTreads) ||
        `move ${snap.move}->${p.moveSpeed}, noFall ${p.noFallDmg}`);
    check('ninho de vespas', () => p.strongBees === true || 'strongBees falso');
    snap.defense = p.statDefense;
    flags.forceDash = true;
}

function dashAndDebuff() {
    const p = me();
    check('dash: impulso e rastro', () => {
        const dash = p.GetModPlayer('ExampleDashPlayer');
        return (spy.dashVelocity >= 10 && dash.DashTimer > 0 && p.eocDash > 0) ||
            `velocidade ${spy.dashVelocity}, timer ${dash.DashTimer}, eocDash ${p.eocDash}`;
    });
    p['void AddBuff(int type, int time, bool fromNetPvP)'](DEBUFF, 300, false);
}

function debuffCheck() {
    const p = me();
    check('debuff: defesa -25% (buff -> ModPlayer -> UpdateEquips)', () => {
        const want = Math.floor(snap.defense * 0.75);
        return (p.GetModPlayer('ExamplePlayer').ExampleDefenseDebuff && Math.abs(p.statDefense - want) <= 1) ||
            `antes ${snap.defense}, agora ${p.statDefense}, esperado ${want}`;
    });
    const i = p['int FindBuffIndex(int type)'](DEBUFF);
    if (i >= 0) p['void DelBuff(int b)'](i);
    restoreSlots();
    equip(p, 15, BOOTS);
}

function vanityCheck() {
    const p = me();
    check('botas na vaidade: so o visual', () =>
        (p.hellfireTreads === true && p.noFallDmg === snap.noFall) || `hellfire ${p.hellfireTreads}, noFall ${p.noFallDmg}`);
    restoreSlots();
    // Um segundo jogador de verdade no mundo, com o escudo; o local, sem.
    p2 = Main.player[1];
    p2.position = p.position;
    p2.active = true;
    equip(p2, 3, SHIELD);
    spy.resetOf.clear();
}

function secondPlayerCheck() {
    const p = me();
    check('segundo jogador: ganchos e estado proprios', () => {
        const mine = p.GetModPlayer('ExampleDashPlayer'), theirs = p2.GetModPlayer('ExampleDashPlayer');
        if (!(spy.resetOf.get(1) > 0)) return 'ResetEffects do jogador 1: ' + spy.resetOf.get(1);
        return (mine !== theirs && theirs.DashAccessoryEquipped === true && mine.DashAccessoryEquipped === false &&
                theirs.Player === p2) ||
            `local ${mine.DashAccessoryEquipped}, jogador 1 ${theirs.DashAccessoryEquipped}`;
    });
    restoreSlots();
    p2.active = false;
}

function killChecks() {
    const p = me();
    check('PreKill false: nao morre', () => {
        flags.blockKill = true;
        p['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'](reason(), 9999, 0, false);
        flags.blockKill = false;
        return (!p.dead && spy.kill === 0) || `morto ${p.dead}, Kill ${spy.kill}`;
    });
    check('Kill', () => {
        p['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'](reason(), 9999, 0, false);
        return (p.dead && spy.kill === 1) || `morto ${p.dead}, Kill ${spy.kill}`;
    });
    p.respawnTimer = 2;
}

function respawnCheck() {
    const p = me();
    check('OnRespawn', () => (!p.dead && spy.respawn === 1) || `morto ${p.dead}, OnRespawn ${spy.respawn}`);
    check('instancia sobrevive a morte', () => {
        const a = p.GetModPlayer(TestPlayer);
        return (a.note === 'local' && a.initCount === 1 && a.Player === p) || `note ${a.note}, init ${a.initCount}`;
    });
    bl.log('modplayer FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 1) { Main.dayTime = true; Main.time = 27000; }
    if (frames < 300 && !self.dead) self.statLife = self.statLifeMax2;
    if (frames === 30) identity();
    if (frames === 40) flags.captureNext = true;
    if (frames === 43) orderCheck();
    if (frames === 45) { snap.lifeMax = self.statLifeMax2; flags.bonusLife = 50; }
    if (frames === 47) numbersChecks();
    if (frames === 55) hurtChecks();
    if (frames === 60) equipExample();
    if (frames === 63) exampleChecks();
    if (frames === 64) dashAndDebuff();
    if (frames === 68) debuffCheck();
    if (frames === 71) vanityCheck();
    if (frames === 76) secondPlayerCheck();
    // Morrer por ultimo: os outros testes da bateria usam o jogador ate ~270.
    if (frames === 290) saveCheck();
    if (frames === 300) killChecks();
    if (frames === 420) respawnCheck();
});
bl.log('modplayer: carregado');
