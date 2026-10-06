const Main = Terraria.Main, N = Terraria.NPC, Pr = Terraria.Projectile;
const owned = [], shots = [];
let checks = 0, failures = 0, frames = 0, done = false, player, target, second, sourceSeen, profileSeen;
const source = Terraria.DataStructures.EntitySource_DebugCommand.new(); source['void .ctor()']();
const spawnNPC = N['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const spawnProjectile = Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
function log(text) { bl.log('modnpchooks ' + text); }
function check(name, test) {
    checks++;
    try { if (!test()) throw Error('resultado falso'); log(name + ': ok'); }
    catch (error) { failures++; log(name + ': FALHOU ' + error); }
}
function record(m, name) { if (!m.calls) m.calls = {}; m.calls[name] = (m.calls[name] || 0) + 1; }
export class NPCProbe extends ModNPC {
    Texture = 'Box';
    HideFromBestiary = true;
    HideFromModMenu = true;
    calls = null;
    mode = '';
    SetStaticDefaults() { Main.npcFrameCount[this.Type] = 1; }
    SetDefaults(npc) {
        this.calls = {};
        npc.width = npc.height = 16; npc.aiStyle = -1; npc.damage = 1; npc.defense = 0;
        npc.lifeMax = 100000; npc.noGravity = true; npc.noTileCollide = true; npc.knockBackResist = 0;
        npc.npcSlots = 0; npc.value = 0;
    }
    OnSpawn(npc, src) { record(this, 'OnSpawn'); this.source = bl.addressOf(src); sourceSeen = src; }
    ResetEffects(npc) { record(this, 'ResetEffects'); this.resetRegen = npc.lifeRegen; this.tickState = 0; }
    AI(npc) { this.tickState = 7; }
    CheckActive(npc) { return false; }
    CheckDead(npc) {
        record(this, 'CheckDead');
        if (this.mode === 'deathVeto') { npc.life = 91; return false; }
        return true;
    }
    ApplyDifficultyAndPlayerScaling(npc, count, balance, adjustment) {
        record(this, 'ApplyDifficultyAndPlayerScaling'); this.scaling = { count, balance, adjustment };
        if (this.mode === 'scaling') npc.lifeMax += 17;
    }
    PreKill(npc) { return false; }
    CanBeHitByItem(npc, owner, item) { record(this, 'CanBeHitByItem'); if (this.mode === 'itemVeto') return false; }
    ModifyIncomingHit(npc, mod) {
        record(this, 'ModifyIncomingHit');
        if (this.mode === 'incoming') { mod.damage = 7; mod.SetCrit(); mod.hitDirection = -1; }
    }
    OnHitByItem(npc, owner, item, hit, damage) {
        record(this, 'OnHitByItem'); this.item = item; this.itemPlayer = owner; this.itemHit = hit; this.itemDamage = damage;
    }
    OnHitByProjectile(npc, projectile, hit, damage) {
        record(this, 'OnHitByProjectile'); this.projectile = projectile; this.projectileHit = hit; this.projectileDamage = damage;
    }
    CanHitPlayer(npc, owner, slot) {
        record(this, 'CanHitPlayer'); this.slot = slot.value;
        if (this.mode === 'contactVeto') return false;
        if (this.mode === 'contact') slot.value = 0;
        return true;
    }
    ModifyHitPlayer(npc, owner, mod) { record(this, 'ModifyHitPlayer'); if (this.mode === 'contact') mod.damage = 3; }
    OnHitPlayer(npc, owner, info) { record(this, 'OnHitPlayer'); this.hurtInfo = info; this.hurtPlayer = owner; }
    TownNPCProfile() {
        record(this, 'TownNPCProfile');
        profileSeen = Terraria.GameContent.TownNPCProfiles.Instance._townNPCProfiles.get_Item(22);
        return profileSeen;
    }
    CanChat(npc) { record(this, 'CanChat'); return this.mode === 'chatYes'; }
    AddShops() { new NPCShop(this.Type, 'Probe').Add(Terraria.ID.ItemID.WoodenSword).Register(); }
    ModifyActiveShop(npc, name, items) {
        record(this, 'ModifyActiveShop'); this.shopName = name; this.shopItems = items;
        items[0].shopCustomPrice = 123;
    }
    ModifyNPCHappiness(npc, owner, biome, helper, nearby) {
        record(this, 'ModifyNPCHappiness'); this.happiness = { owner, biome, nearby };
        helper._currentPriceAdjustment = .95;
    }
    DrawEffects(npc, color) { record(this, 'DrawEffects'); if (this.mode === 'drawColor') color.value = Color.new(71, 82, 93, 255); }
    PreDraw(npc, batch, screen, color) { record(this, 'PreDraw'); this.preR = color.R; return this.mode === 'drawColor'; }
    PostDraw(npc, batch, screen, color) { record(this, 'PostDraw'); this.postR = color.R; }
    DrawBehind(npc, index) {
        record(this, 'DrawBehind'); this.drawIndex = index;
        Main.instance.DrawCacheNPCsOverPlayers.Add(index);
    }
    GetAlpha(npc, color) {
        record(this, 'GetAlpha'); this.alphaR = color.R;
        if (this.mode === 'alpha') return Color.new(11, 22, 33, 0);
    }
    BossHeadSlot(npc, slot) { record(this, 'BossHeadSlot'); if (this.mode === 'head') slot.value = -1; }
    BossHeadRotation(npc, rotation) { record(this, 'BossHeadRotation'); if (this.mode === 'head') rotation.value = .75; }
    BossHeadSpriteEffects(npc, effects) { record(this, 'BossHeadSpriteEffects'); if (this.mode === 'head') effects.value = 1; }
}
export class NPCProbePlain extends ModNPC {
    Texture = 'Box';
    HideFromBestiary = true;
    HideFromModMenu = true;
    SetStaticDefaults() { Main.npcFrameCount[this.Type] = 1; }
    SetDefaults(npc) {
        npc.width = npc.height = 16; npc.aiStyle = -1; npc.damage = 0; npc.defense = 0; npc.lifeMax = 10000;
        npc.noGravity = true; npc.noTileCollide = true; npc.npcSlots = 0; npc.value = 0;
    }
}
export class NPCProbeShot extends ModProjectile {
    Texture = 'Box';
    SetDefaults(p) { p.width = p.height = 12; p.aiStyle = -1; p.friendly = true; p.tileCollide = false; p.penetrate = -1; p.timeLeft = 600; }
}
function spawn(type, dx = 80) {
    const c = player.Center;
    const index = spawnNPC(source, Math.floor(c.X + dx), Math.floor(c.Y - 80), type, 0, 0, 0, 0, 0, 255);
    if (index < 0 || index >= 200) throw Error('sem slot NPC');
    const npc = Main.npc[index]; owned.push(npc); return npc;
}
function clearImmunity(npc) { for (let i = 0; i < npc.immune.length; i++) npc.immune[i] = 0; }
function itemHit(npc, weapon) {
    clearImmunity(npc);
    player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](weapon, npc.Hitbox, 40, 0, npc.whoAmI);
}
function projectileHit(npc, owner) {
    const c = npc.Center;
    const index = spawnProjectile(source, c.X, c.Y, 0, 0, ModProjectile.getTypeByName('NPCProbeShot'), 20, 0, owner, 0, 0, 0, null);
    const p = Main.projectile[index]; shots.push(p); p.Center = npc.Center;
    clearImmunity(npc);
    p['void Damage_PVE(ref Rectangle projRectangle, float projectileSpecificDamageMultiplier)'](new Ref(p.Hitbox), 1);
    p.active = false; return p;
}
function prepare() {
    check('execucao singleplayer', () => Main.netMode === 0);
    target = spawn(ModNPC.getTypeByName('NPCProbe')); second = spawn(ModNPC.getTypeByName('NPCProbe'), 140);
    const m = target.ModNPC, sm = second.ModNPC;
    check('OnSpawn uma vez e fonte nativa correta', () => m.calls.OnSpawn === 1 && m.source === bl.addressOf(source) && !!sourceSeen);
    check('estado por entidade isolado', () => m !== sm && m.calls !== sm.calls);
    check('perfil nativo registrado apos conteudo', () => !!profileSeen && bl.addressOf(Terraria.GameContent.TownNPCProfiles.Instance._townNPCProfiles.get_Item(target.type)) === bl.addressOf(profileSeen));
    const oldDifficulty = target.difficulty, oldMax = target.lifeMax;
    try {
        target.difficulty = Terraria.DataStructures.GameDifficultyLevel.Master; m.mode = 'scaling';
        target['void ScaleStats_ByPlayerCount(int numPlayers)'](2);
        const balance = new Ref(1), boost = new Ref(1);
        N['void GetStatScalingFactors(int numPlayers, out float balance, out float boost)'](2, balance, boost);
        check('escalonamento usa count real, balance nativo e ajuste master', () => m.scaling && m.scaling.count === 2 && Math.abs(m.scaling.balance - balance.value) < .00001 && Math.abs(m.scaling.adjustment - .85) < .00001 && target.lifeMax >= oldMax + 17);
    } finally { target.difficulty = oldDifficulty; target.lifeMax = oldMax; target.life = oldMax; }
    m.mode = 'chatYes';
    check('CanChat habilita NPC fora da AI de morador', () => target['bool get_CanTalk()']() && target['bool get_CanBeTalkedTo()']());
    m.mode = 'chatNo';
    check('CanChat false bloqueia conversa', () => !target['bool get_CanTalk()']() && !target['bool get_CanBeTalkedTo()']());
    m.mode = 'head';
    check('BossHeadSlot oculta icone', () => target.GetBossHeadTextureIndex() === -1);
    check('BossHeadRotation ref', () => Math.abs(target.GetBossHeadRotation() - .75) < .00001);
    check('BossHeadSpriteEffects ref', () => target.GetBossHeadSpriteEffects() === 1);
    m.mode = 'alpha';
    check('GetAlpha aceita transparencia total', () => { const c = target.GetAlpha(Color.White); return c.R === 11 && c.A === 0; });
    m.mode = 'deathVeto'; target.life = 0; target['void checkDead()']();
    check('CheckDead false mantem NPC e vida restaurada', () => target.active && target.life === 91 && m.calls.CheckDead === 1);
    target.life = target.lifeMax; m.mode = 'incoming';
    const life = target.life;
    const damage = target['int StrikeNPCNoInteraction(int Damage, float knockBack, int hitDirection)'](40, 0, 1);
    check('ModifyIncomingHit altera dano e crit em golpe direto', () => damage === 14 && life - target.life === 14);
    const weapon = Terraria.Item.new(); weapon['void .ctor()'](); weapon['void SetDefaults(int Type, ItemVariant variant)'](Terraria.ID.ItemID.WoodenSword, null);
    m.mode = 'itemVeto'; const before = target.life; itemHit(target, weapon);
    check('CanBeHitByItem false impede dano e notificacao', () => target.life === before && !m.calls.OnHitByItem && m.calls.CanBeHitByItem > 0);
    m.mode = 'incoming'; const itemBefore = target.life; itemHit(target, weapon);
    check('OnHitByItem chamado uma vez com entidades certas', () => m.calls.OnHitByItem === 1 && bl.addressOf(m.item) === bl.addressOf(weapon) && bl.addressOf(m.itemPlayer) === bl.addressOf(player));
    check('HitInfo de item reflete modificador e dano nativo', () => m.itemHit && m.itemHit.SourceDamage === 7 && m.itemHit.Crit && m.itemDamage === itemBefore - target.life && m.itemHit.Damage === m.itemDamage);
    m.mode = ''; const shot = projectileHit(target, Main.myPlayer);
    check('OnHitByProjectile chamado uma vez com proj correto', () => m.calls.OnHitByProjectile === 1 && bl.addressOf(m.projectile) === bl.addressOf(shot) && m.projectileDamage > 0 && m.projectileHit.Damage === m.projectileDamage);
    const trap = projectileHit(target, 255);
    check('OnHitByProjectile tambem sem dono jogador', () => m.calls.OnHitByProjectile === 2 && bl.addressOf(m.projectile) === bl.addressOf(trap));
    const fields = ['statLife', 'immune', 'immuneTime'];
    const snapshot = fields.map(key => player[key]), cooldowns = [player.hurtCooldowns[0], player.hurtCooldowns[1]];
    const hurtSource = Terraria.DataStructures.PlayerDeathReason['PlayerDeathReason ByNPC(int index)'](target.whoAmI);
    const hurt = () => player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'](hurtSource, 3, 1, false, true, false, -1, false);
    try {
        m.mode = 'contactVeto'; const hp = player.statLife; const result = hurt();
        check('CanHitPlayer false impede Hurt', () => result === 0 && player.statLife === hp && !m.calls.OnHitPlayer);
        player.immune = false; player.hurtCooldowns[0] = player.hurtCooldowns[1] = 0; m.mode = 'contact';
        const damageDone = hurt();
        check('ModifyHitPlayer e OnHitPlayer no Hurt real', () => damageDone > 0 && m.calls.ModifyHitPlayer === 1 && m.calls.OnHitPlayer === 1 && bl.addressOf(m.hurtPlayer) === bl.addressOf(player));
        check('cooldown Ref e HurtInfo correspondem ao dano', () => m.hurtInfo && m.hurtInfo.CooldownCounter === 0 && m.hurtInfo.Damage === damageDone);
    } finally {
        for (let i = 0; i < fields.length; i++) player[fields[i]] = snapshot[i];
        for (let i = 0; i < cooldowns.length; i++) player.hurtCooldowns[i] = cooldowns[i];
    }
    const oldTalk = player.talkNPC;
    try {
        player.talkNPC = target.whoAmI;
        const store = Terraria.InventoryStorage.new(); store['void .ctor()']();
        store['void SetupShop(int type)'](NPCShop.get(target.type, 'Probe').Index);
        check('ModifyActiveShop recebe itens e nome apos preenchimento', () => m.calls.ModifyActiveShop === 1 && m.shopName === 'Probe' && m.shopItems[0].type === Terraria.ID.ItemID.WoodenSword && m.shopItems[0].shopCustomPrice === 123);
    } finally { player.talkNPC = oldTalk; }
    const town = target.townNPC, homeless = target.homeless, hx = target.homeTileX, hy = target.homeTileY;
    try {
        target.townNPC = true; target.homeless = false; target.homeTileX = Math.floor(target.Center.X / 16); target.homeTileY = Math.floor(target.Center.Y / 16);
        const helper = Terraria.GameContent.ShopHelper.new(); helper['void .ctor()']();
        const settings = helper['ShoppingSettings GetShoppingSettings(Player player, NPC npc)'](player, target);
        check('felicidade participa do calculo nativo de preco', () => m.calls.ModifyNPCHappiness === 1 && m.happiness && bl.addressOf(m.happiness.owner) === bl.addressOf(player) && !!m.happiness.nearby && Math.abs(settings.PriceAdjustment - .95) < .00001);
    } finally { target.townNPC = town; target.homeless = homeless; target.homeTileX = hx; target.homeTileY = hy; }
    const plain = spawn(ModNPC.getTypeByName('NPCProbePlain'), -120);
    check('tipo sem sobrescritas conserva cor nativa', () => plain.GetAlpha(Color.White).A > 0);
    m.mode = 'drawVeto'; sm.mode = 'drawVeto';
}
function cleanup() { done = true; for (const npc of owned) npc.active = false; for (const p of shots) p.active = false; }
function finish() {
    const m = target.ModNPC;
    check('ResetEffects por tick depois de reset nativo', () => m.calls.ResetEffects > 10 && m.resetRegen === 0 && m.tickState === 7);
    check('DrawBehind recebe indice e roda no cache de desenho', () => m.calls.DrawBehind > 0 && m.drawIndex === target.whoAmI);
    check('PreDraw false conserva PostDraw no desenho real', () => m.calls.PreDraw > 0 && m.calls.PostDraw === m.calls.PreDraw);
    check('DrawEffects executa antes do desenho', () => m.calls.DrawEffects === m.calls.PreDraw);
    m.mode = 'drawColor';
}
function report() {
    const m = target.ModNPC;
    check('DrawEffects Ref chega a PreDraw e PostDraw', () => m.preR === 71 && m.postR === 71);
    check('cor modificada chega a GetAlpha nativo', () => m.alphaR === 71);
    const samples = [], before = m.calls.ResetEffects;
    const reset = target['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'];
    for (let r = 0; r < 5; r++) {
        const start = performance.now(); for (let i = 0; i < 5000; i++) reset(false);
        samples.push((performance.now() - start) * 1000 / 5000);
    }
    samples.sort((a, b) => a - b);
    check('25000 resets mantem instancia e callbacks sem perda', () => target.ModNPC === m && m.calls.ResetEffects === before + 25000);
    log('benchmark ResetEffects mediana_us=' + samples[2].toFixed(3));
    cleanup(); log('FIM checks=' + checks + ' falhas=' + failures);
}
Terraria.Player['void Update(int i)'].hook((original, self, index) => {
    original(self, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;
    try {
        if (frames === 60) { player = self; prepare(); }
        if (frames === 110) finish();
        if (frames === 125) report();
    } catch (error) { failures++; log('execucao: FALHOU ' + error + ' ' + error.stack); cleanup(); log('FIM checks=' + checks + ' falhas=' + failures); }
});
export default class NPCProbeTests extends Mod {}
