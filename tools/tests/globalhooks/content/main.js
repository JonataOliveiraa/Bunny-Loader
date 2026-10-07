// Globais nos NPCs, itens e projéteis do jogo (um jogador): os eventos do
// GlobalNPC (FindFrame, CheckDead, CanChat, GetChat, GetAlpha, ícone de
// chefe), as lojas (SetupShop, ModifyShop, ModifyActiveShop, mercador
// viajante), o GlobalItem (crítico, repulsão) e os golpes pelo CombatLoader:
// item, projétil e NPC contra NPC no NPC, projétil hostil e PvP no jogador,
// e o HitModifiers (FinalDamage, ArmorPenetration, SetMaxDamage,
// SetInstantKill). Loga "globalhooks ...".
const Main = Terraria.Main, N = Terraria.NPC, Pr = Terraria.Projectile;
const { ItemID } = Terraria.ID;
const owned = [], shots = [];
const state = { calls: {}, mode: {} };
let checks = 0, failures = 0, frames = 0, done = false, player, slime, merchant;
const source = Terraria.DataStructures.EntitySource_DebugCommand.new(); source['void .ctor()']();
const spawnNPC = N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const spawnProjectile = Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

function log(text) { bl.log('globalhooks ' + text); }
function check(name, test) {
    checks++;
    try {
        const r = test();
        if (r !== true) throw Error(r === false || r === undefined ? 'resultado falso' : String(r));
        log(name + ': ok');
    } catch (error) { failures++; log(name + ': FALHOU ' + error); }
}
function rec(name) { state.calls[name] = (state.calls[name] || 0) + 1; }
const calls = (name) => state.calls[name] || 0;
const is = (npc, other) => !!npc && !!other && npc.whoAmI === other.whoAmI;

export class GProbeNPC extends GlobalNPC {
    OnSpawn(npc, src) { if (npc.type === 1) rec('OnSpawn'); }
    FindFrame(npc, height) { if (is(npc, slime)) rec('FindFrame'); }
    CheckActive(npc) { if (is(npc, slime)) { rec('CheckActive'); return false; } return true; }
    CheckDead(npc) {
        if (!is(npc, slime) || !state.mode.dead) return true;
        rec('CheckDead'); npc.life = 1; return false;
    }
    CanChat(npc) { return npc.type === 17 && state.mode.noChat ? false : null; }
    GetChat(npc, chat) { if (npc.type === 17) chat.value = 'oi global'; }
    GetAlpha(npc, color) { return is(npc, slime) && state.mode.alpha ? Color.new(11, 0, 0, 0) : null; }
    BossHeadSlot(npc, index) { if (is(npc, slime) && state.mode.head) index.value = 7; }

    SetupShop(type, shop, nextSlot) {
        if (type !== 17) return;
        rec('SetupShop');
        shop.item[nextSlot.value]['void SetDefaults(int Type, ItemVariant variant)'](ItemID.Torch, null);
        nextSlot.value++;
    }
    ModifyShop(shop) { if (shop.NpcType === 17) { rec('ModifyShop'); shop.Add(ItemID.Wood); } }
    ModifyActiveShop(npc, name, items) { if (npc.type === 17) { rec('ModifyActiveShop'); state.shopName = name; } }
    SetupTravelShop(shop, nextSlot) { rec('SetupTravelShop'); shop[nextSlot.value] = ItemID.Gel; nextSlot.value++; }

    CanBeHitByItem(npc, owner, item) { return is(npc, slime) && state.mode.itemVeto ? false : null; }
    ModifyHitByItem(npc, owner, item, modifiers) {
        if (!is(npc, slime)) return;
        rec('ModifyHitByItem');
        if (state.mode.final) modifiers.FinalDamage.Multiplicative *= 2;
    }
    OnHitByItem(npc, owner, item, hit, damageDone) { if (is(npc, slime)) { rec('OnHitByItem'); state.itemHit = hit; state.itemDone = damageDone; } }
    CanBeHitByProjectile(npc, projectile) { return is(npc, slime) && state.mode.projVeto ? false : null; }
    ModifyHitByProjectile(npc, projectile, modifiers) { if (is(npc, slime)) rec('ModifyHitByProjectile'); }
    OnHitByProjectile(npc, projectile, hit, damageDone) { if (is(npc, slime)) { rec('OnHitByProjectile'); state.npcProjDone = damageDone; } }
    ModifyIncomingHit(npc, modifiers) {
        if (!is(npc, slime)) return;
        rec('ModifyIncomingHit');
        if (state.mode.maxDamage) modifiers.SetMaxDamage(3);
        if (state.mode.pen) modifiers.ArmorPenetration += 20;
        if (state.mode.kill) modifiers.SetInstantKill();
    }
    CanHitNPC(npc, target) {
        if (!is(npc, state.attacker)) return true;
        rec('CanHitNPC');
        return !state.mode.npcVeto;
    }
    ModifyHitNPC(npc, target, modifiers) { if (is(npc, state.attacker)) { rec('ModifyHitNPC'); modifiers.SourceDamage.Flat += 5; } }
    OnHitNPC(npc, target, hit) { if (is(npc, state.attacker)) { rec('OnHitNPC'); state.npcHit = hit; } }
}

export class GProbeItem extends GlobalItem {
    ModifyWeaponCrit(item, owner, crit) { if (item.type === ItemID.WoodenSword && state.mode.crit) crit.value += 50; }
    ModifyWeaponKnockback(item, owner, knockback) { if (item.type === ItemID.WoodenSword && state.mode.crit) knockback.Flat += 3; }
    ModifyHitNPC(item, owner, target, modifiers) { if (item.type === ItemID.WoodenSword) rec('Item.ModifyHitNPC'); }
    OnHitNPC(item, owner, target, hit, damageDone) { if (item.type === ItemID.WoodenSword) { rec('Item.OnHitNPC'); state.gItemDone = damageDone; } }
}

export class GProbeProjectile extends GlobalProjectile {
    CanHitNPC(p, target) { return is(p, state.shot) && state.mode.gProjVeto ? false : null; }
    ModifyHitNPC(p, target, modifiers) { if (is(p, state.shot)) { rec('Proj.ModifyHitNPC'); modifiers.SourceDamage.Flat += 10; } }
    OnHitNPC(p, target, hit, damageDone) { if (is(p, state.shot)) { rec('Proj.OnHitNPC'); state.projHit = hit; state.projDone = damageDone; } }
    CanHitPlayer(p, target) { return !(is(p, state.hostile) && state.mode.hostileVeto); }
    ModifyHitPlayer(p, target, modifiers) { if (is(p, state.hostile)) { rec('Proj.ModifyHitPlayer'); modifiers.FinalDamage.Flat += 2; } }
    OnHitPlayer(p, target, info) { if (is(p, state.hostile)) { rec('Proj.OnHitPlayer'); state.hostileInfo = info; } }
    CanHitPvp(p, target) { return !(is(p, state.hostile) && state.mode.pvpVeto); }
}

export class GProbePlayer extends ModPlayer {
    CanHitPvpWithProj(self, projectile, target) { rec('CanHitPvpWithProj'); return true; }
}

function spawn(type, dx = 80) {
    const c = player.Center;
    const index = spawnNPC(source, Math.floor(c.X + dx), Math.floor(c.Y - 80), type, 0, 0, 0, 0, 0, 255);
    if (index < 0 || index >= 200) throw Error('sem slot NPC');
    const npc = Main.npc[index]; owned.push(npc); return npc;
}
function still(npc) {
    npc.aiStyle = -1; npc.noGravity = true; npc.noTileCollide = true; npc.knockBackResist = 0;
    npc.lifeMax = 100000; npc.life = 100000; npc.defense = 0; npc.takenDamageMultiplier = 1;
}
function clearImmunity(npc) { for (let i = 0; i < npc.immune.length; i++) npc.immune[i] = 0; }
function swordHit(npc, sword) {
    clearImmunity(npc);
    const before = npc.life;
    player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](sword, npc.Hitbox, 40, 0, npc.whoAmI);
    return before - npc.life;
}
function arrowHit(npc) {
    const c = npc.Center;
    const index = spawnProjectile(source, c.X, c.Y, 0, 0, 1, 20, 0, Main.myPlayer, 0, 0, 0, null);
    const p = Main.projectile[index]; shots.push(p); p.Center = npc.Center; p.penetrate = -1;
    state.shot = p;
    clearImmunity(npc);
    const before = npc.life;
    p['void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)'](new Ref(p.Hitbox), 1);
    p.active = false;
    return before - npc.life;
}
function hostileHurt(pvp) {
    const c = player.Center;
    const index = spawnProjectile(source, c.X, c.Y, 0, 0, 55, 10, 0, pvp ? Main.myPlayer : 255, 0, 0, 0, null);
    const p = Main.projectile[index]; shots.push(p); state.hostile = p;
    const src = Terraria.DataStructures.PlayerDeathReason['PlayerDeathReason ByProjectile(int playerIndex, int projectileIndex)'](pvp ? Main.myPlayer : -1, index);
    const life = player.statLife;
    player.immune = false; player.immuneTime = 0;
    for (let i = 0; i < player.hurtCooldowns.length; i++) player.hurtCooldowns[i] = 0;
    const result = player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'](src, 10, 1, pvp, true, false, -1, false);
    const lost = life - player.statLife;
    player.statLife = life; player.immune = false; player.immuneTime = 0;
    p.active = false;
    return { result, lost };
}

function prepare() {
    check('execucao singleplayer', () => Main.netMode === 0);
    slime = spawn(1); still(slime);
    merchant = spawn(17, 160); still(merchant);
    check('OnSpawn do Global num NPC do jogo', () => calls('OnSpawn') >= 1);

    slime['void CheckActive()']();
    check('CheckActive do Global num NPC do jogo', () => calls('CheckActive') >= 1 && slime.active);
    state.mode.dead = true; slime.life = 0; slime['void checkDead()']();
    check('CheckDead false mantem o NPC do jogo', () => slime.active && calls('CheckDead') === 1);
    state.mode.dead = false; still(slime);
    state.mode.alpha = true;
    check('GetAlpha do Global', () => slime['Color GetAlpha(Color newColor)'](Color.White).R === 11);
    state.mode.alpha = false; state.mode.head = true;
    check('BossHeadSlot do Global', () => slime.GetBossHeadTextureIndex() === 7);
    state.mode.head = false;
    check('GetChat do Global no Comerciante', () => merchant['string GetChat()']() === 'oi global');
    state.mode.noChat = true;
    check('CanChat false do Global', () => !merchant['bool get_CanTalk()']());
    state.mode.noChat = false;

    const oldTalk = player.talkNPC;
    try {
        player.talkNPC = merchant.whoAmI;
        // O jogo do celular monta todas as lojas ao abrir o mundo: conta a diferença.
        const legacy0 = calls('SetupShop'), active0 = calls('ModifyActiveShop');
        const store = Terraria.InventoryStorage.new(); store['void .ctor()']();
        store['void SetupShop(int type)'](1);
        const types = []; for (let i = 0; i < store.item.length; i++) if (store.item[i].type > 0) types.push(store.item[i].type);
        check('ModifyShop: o Add entra depois dos itens do jogo', () => (calls('ModifyShop') === 1 && types.includes(ItemID.Wood) && types[0] !== ItemID.Wood) || JSON.stringify(types));
        check('SetupShop (formato antigo) acrescenta na casa livre', () => (calls('SetupShop') === legacy0 + 1 && types[types.length - 1] === ItemID.Torch) || JSON.stringify(types) + ' ' + JSON.stringify(state.calls));
        check('ModifyActiveShop com o nome da loja', () => calls('ModifyActiveShop') === active0 + 1 && state.shopName === 'Shop');
        store['void SetupShop(int type)'](1);
        check('ModifyShop uma vez por loja', () => (calls('ModifyShop') === 1 && calls('SetupShop') === legacy0 + 2) || JSON.stringify(state.calls));
    } finally { player.talkNPC = oldTalk; }
    const travel0 = calls('SetupTravelShop');
    Terraria.InventoryStorage['void SetupTravelShop()']();
    check('SetupTravelShop do Global', () => {
        const shop = Main.travelShop;
        const list = []; for (let i = 0; i < shop.length; i++) list.push(shop[i]);
        return (list.includes(ItemID.Gel) && calls('SetupTravelShop') === travel0 + 1) || JSON.stringify(list) + ' ' + calls('SetupTravelShop');
    });

    const sword = Terraria.Item.new(); sword['void .ctor()'](); sword['void SetDefaults(int Type, ItemVariant variant)'](ItemID.WoodenSword, null);
    const crit0 = player['int GetWeaponCrit(Item sItem)'](sword), kb0 = player['float GetWeaponKnockback(Item sItem, float KnockBack)'](sword, sword.knockBack);
    state.mode.crit = true;
    const crit1 = player['int GetWeaponCrit(Item sItem)'](sword), kb1 = player['float GetWeaponKnockback(Item sItem, float KnockBack)'](sword, sword.knockBack);
    state.mode.crit = false;
    check('ModifyWeaponCrit do GlobalItem', () => crit1 - crit0 === 50 || `${crit0} -> ${crit1}`);
    check('ModifyWeaponKnockback do GlobalItem', () => Math.abs(kb1 - kb0 - 3) < 1e-4 || `${kb0} -> ${kb1}`);

    state.mode.itemVeto = true;
    check('CanBeHitByItem false do Global impede o golpe', () => swordHit(slime, sword) === 0 && !calls('OnHitByItem'));
    state.mode.itemVeto = false; state.mode.final = true;
    const lost = swordHit(slime, sword);
    const hit = state.itemHit;
    check('FinalDamage dobra o dano (item no NPC do jogo)', () => (!!hit && lost === state.itemDone && state.itemDone === hit.SourceDamage * 2 * (hit.Crit ? 2 : 1)) ||
        `perdeu ${lost}, done ${state.itemDone}, origem ${hit && hit.SourceDamage}, crit ${hit && hit.Crit}`);
    check('GlobalItem ModifyHitNPC e OnHitNPC no mesmo golpe', () => calls('Item.ModifyHitNPC') >= 1 && state.gItemDone === state.itemDone);
    state.mode.final = false; state.mode.maxDamage = true;
    check('SetMaxDamage limita o dano', () => swordHit(slime, sword) === 3);
    state.mode.maxDamage = false;
    slime.defense = 20;
    const plain = swordHit(slime, sword), plainHit = state.itemHit;
    state.mode.pen = true;
    const pierced = swordHit(slime, sword), piercedHit = state.itemHit;
    state.mode.pen = false; slime.defense = 0;
    check('ArmorPenetration tira a defesa', () => (pierced === piercedHit.SourceDamage * (piercedHit.Crit ? 2 : 1) && plain < plainHit.SourceDamage * (plainHit.Crit ? 2 : 1)) ||
        `sem ${plain}/${plainHit.SourceDamage}, com ${pierced}/${piercedHit.SourceDamage}`);

    state.mode.gProjVeto = true;
    check('GlobalProjectile CanHitNPC false impede', () => arrowHit(slime) === 0 && !calls('Proj.OnHitNPC'));
    state.mode.gProjVeto = false;
    const shot = arrowHit(slime);
    check('projétil do jogo: Modify e OnHit do GlobalProjectile e do GlobalNPC', () =>
        (shot > 0 && calls('Proj.ModifyHitNPC') === 1 && calls('Proj.OnHitNPC') === 1 && state.projDone === shot && state.npcProjDone === shot &&
         calls('ModifyHitByProjectile') === 1 && state.projHit.Damage === shot) || `dano ${shot}, ${JSON.stringify(state.calls)}`);

    const attacker = spawn(1, -80); still(attacker); attacker.damage = 10; state.attacker = attacker;
    const victim = merchant;
    clearImmunity(victim); victim.immune[255] = 0;
    state.mode.npcVeto = true;
    let before = victim.life;
    victim['void BeHurtByOtherNPC(int npcIndex, NPC thatNPC)'](attacker.whoAmI, attacker);
    check('CanHitNPC false (NPC contra NPC) impede', () => victim.life === before && calls('CanHitNPC') === 1 && !calls('OnHitNPC'));
    state.mode.npcVeto = false; victim.immune[255] = 0; before = victim.life;
    victim['void BeHurtByOtherNPC(int npcIndex, NPC thatNPC)'](attacker.whoAmI, attacker);
    check('NPC contra NPC: ModifyHitNPC e OnHitNPC', () => (calls('ModifyHitNPC') === 1 && !!state.npcHit && state.npcHit.Damage === before - victim.life && before > victim.life) ||
        `perdeu ${before - victim.life}, ${JSON.stringify(state.calls)}`);

    const slime2 = spawn(1, 40); still(slime2);
    state.mode.kill = true;
    const victim2 = slime; slime = slime2;
    swordHit(slime2, sword);
    check('SetInstantKill mata', () => !slime2.active || slime2.life <= 0);
    slime = victim2; state.mode.kill = false;

    state.mode.hostileVeto = true;
    const vetoed = hostileHurt(false);
    check('GlobalProjectile CanHitPlayer false impede o golpe hostil', () => vetoed.result === 0 && vetoed.lost === 0 && !calls('Proj.OnHitPlayer'));
    state.mode.hostileVeto = false;
    const hurt = hostileHurt(false);
    check('projétil hostil: ModifyHitPlayer e OnHitPlayer com HurtInfo', () => (hurt.result > 0 && calls('Proj.ModifyHitPlayer') === 1 && state.hostileInfo && state.hostileInfo.Damage === hurt.result) ||
        `resultado ${hurt.result}, ${JSON.stringify(state.calls)}`);
    state.mode.pvpVeto = true;
    const pvp = hostileHurt(true);
    check('GlobalProjectile CanHitPvp false impede o golpe PvP', () => pvp.result === 0 && pvp.lost === 0);
    state.mode.pvpVeto = false;
    hostileHurt(true);
    check('ModPlayer CanHitPvpWithProj no golpe PvP', () => calls('CanHitPvpWithProj') >= 1);
}

function finish() {
    check('FindFrame do Global num NPC do jogo', () => calls('FindFrame') > 0);
    done = true;
    for (const npc of owned) npc.active = false;
    for (const p of shots) p.active = false;
    log('FIM checks=' + checks + ' falhas=' + failures);
}

Terraria.Player['void Update(int i)'].hook((original, self, index) => {
    original(self, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;
    try {
        if (frames === 60) { player = self; prepare(); }
        if (frames === 90) finish();
    } catch (error) {
        failures++; log('execucao: FALHOU ' + error + ' ' + error.stack);
        done = true; for (const npc of owned) npc.active = false;
        log('FIM checks=' + checks + ' falhas=' + failures);
    }
});
bl.log('globalhooks: carregado');

export default class GlobalHooksTests extends Mod {}
