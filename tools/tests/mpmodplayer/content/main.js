const Main = Terraria.Main, P = Terraria.Player;
const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const events = [], deltas = [], received = [], acknowledgements = [];
let mod, frames = 0, role = '', failures = 0, cases = 0, started = -1, finished = false;
let remote = -1, target = -1, clientFinished = false, hostSync = false, diagnosed = false;

function packet(kind, values, to = -1) {
    const message = mod.GetPacket();
    message.Write(kind);
    for (const value of values) message.Write(value);
    message.Send(to);
}

export default class MultiplayerPlayerTest extends Mod {
    Load() { mod = this; this.GetPacket(); }
    HandlePacket(reader, from) {
        const kind = reader.ReadString();
        if (kind === 'hello' && (Main.netMode & 2)) {
            remote = from;
            if (started < 0) started = frames;
            packet(finished ? 'rejoin' : 'hello', [Main.myPlayer], from);
            if (finished) {
                check('reconexao chama PlayerConnect', () => events.filter(e => e[0] === 'PlayerConnect' && e[1] === from).length >= 2);
                check('desconexao chama PlayerDisconnect uma vez', () => events.filter(e => e[0] === 'PlayerDisconnect' && e[1] === from).length === 1);
                check('desconexao nao dispara para o host local', () => !events.some(e => e[0] === 'PlayerDisconnect' && e[1] === Main.myPlayer));
                check('SyncPlayer repete na reconexao', () => events.filter(e => e[0] === 'SyncPlayer' && e[1] === Main.myPlayer).length >= 2);
                bl.log('mpmodplayer host REJOIN FIM falhas=' + failures);
            }
        } else if (kind === 'rejoin' && Main.netMode === 1) {
            finished = true;
            bl.log('mpmodplayer cliente REJOIN FIM');
        } else if (kind === 'hello' && Main.netMode === 1) {
            remote = reader.ReadInt32();
            if (started < 0) started = frames;
        } else if (kind === 'state') {
            const index = reader.ReadInt32(), revision = reader.ReadInt32(), value = reader.ReadInt32();
            if ((Main.netMode & 2) && index !== from) {
                check('origem do estado validada', () => false);
                return;
            }
            received.push({ index, revision, value, from });
            if (index !== Main.myPlayer) Probe.get(Main.player[index]).payload = { revision, bag: { value } };
            if (Main.netMode & 2) packet('ack', [revision, value], from);
            else if (index !== Main.myPlayer && revision === 7 && value === 700) hostSync = true;
        } else if (kind === 'ack') {
            acknowledgements.push([reader.ReadInt32(), reader.ReadInt32(), from]);
        } else if (kind === 'target') {
            target = reader.ReadInt32();
        } else if (kind === 'finished') {
            const count = reader.ReadInt32();
            clientFinished = true;
            check('cliente termina sem falhas', () => count === 0);
        }
    }
}

export class Probe extends ModPlayer {
    Initialize() {
        this.counts = {};
        this.payload = { revision: 0, bag: { value: 0 } };
        this.crit = 0; this.knockback = 1; this.scale = 1;
        this.speed = 1; this.time = 1; this.animation = 1; this.mana = 1; this.fishing = 0;
        this.blockItem = false; this.blockPotion = false; this.blockTeleport = false;
        this.blockAmmo = false; this.blockShoot = false; this.blockProjectile = false; this.blockJumpVisuals = false;
        this.dodge = false; this.blockIncoming = false; this.hitBonus = 0; this.blockJump = false;
    }
    ModifyWeaponCrit(player, item, crit) { crit.value += this.crit; }
    ModifyWeaponKnockback(player, item, modifier) { modifier.Multiplicative *= this.knockback; }
    ModifyItemScale(player, item, value) { value.value *= this.scale; }
    UseSpeedMultiplier() { return this.speed; }
    UseTimeMultiplier() { return this.time; }
    UseAnimationMultiplier() { return this.animation; }
    ModifyManaCost(player, item, reduce, mult) { mult.value *= this.mana; }
    OnConsumeMana(player, item, amount) { this.lastConsumption = amount; }
    PreItemCheck() { return !this.blockItem; }
    ApplyPotionDelay() { return !this.blockPotion; }
    CanBeTeleportedTo() { return !this.blockTeleport; }
    GetFishingLevel(player, rod, bait, level) { level.value += this.fishing; }
    CanConsumeAmmo() { return !this.blockAmmo; }
    CanShoot() { return !this.blockShoot; }
    ModifyShootStats(player, item, position, velocity, type, damage) { if (this.blockProjectile) damage.value = 42; }
    Shoot(player, item, source, position, velocity, type, damage) { this.lastShotDamage = damage; return !this.blockProjectile; }
    CanShowExtraJumpVisuals() { return !this.blockJumpVisuals; }
    CanStartExtraJump() { return !this.blockJump; }
    ConsumableDodge() { return this.dodge; }
    CanBeHitByNPC() { return !this.blockIncoming; }
    CanBeHitByProjectile() { return !this.blockIncoming; }
    ModifyHitNPC(player, npc, modifiers) { modifiers.SourceDamage.Flat += this.hitBonus; }
    CopyClientState(player, copy) { copy.payload = { revision: this.payload.revision, bag: { value: this.payload.bag.value } }; }
    SendClientChanges(player, previous) {
        if (this.payload.revision === previous.payload.revision && this.payload.bag.value === previous.payload.bag.value) return;
        deltas.push([previous.payload.revision, this.payload.revision]);
        packet('state', [player.whoAmI, this.payload.revision, this.payload.bag.value]);
    }
    SyncPlayer(player, toWho, fromWho, newPlayer) {
        events.push(['SyncPlayer', player.whoAmI, toWho, fromWho, newPlayer]);
        if (Main.netMode & 2) packet('state', [player.whoAmI, this.payload.revision, this.payload.bag.value], toWho);
    }
    PlayerConnect(player) { events.push(['PlayerConnect', player.whoAmI]); }
    PlayerDisconnect(player) {
        events.push(['PlayerDisconnect', player.whoAmI]);
        bl.log('mpmodplayer ' + role + ' DESCONECTOU jogador=' + player.whoAmI);
    }
}

for (const name of Object.getOwnPropertyNames(ModPlayer.prototype)) {
    if (name === 'constructor' || name === 'Player' || name === 'Initialize') continue;
    const implementation = Object.hasOwn(Probe.prototype, name) ? Probe.prototype[name] : ModPlayer.prototype[name];
    Probe.prototype[name] = function (...args) {
        this.counts[name] = (this.counts[name] || 0) + 1;
        return implementation.apply(this, args);
    };
}

function check(name, fn) {
    cases++;
    try {
        if (fn() !== true) throw Error('resultado falso');
        bl.log('mpmodplayer ' + role + ' ' + name + ': ok');
    } catch (error) {
        failures++;
        bl.log('mpmodplayer ' + role + ' ' + name + ': FALHOU ' + error);
    }
}
function sample(type) {
    const item = Terraria.Item.new();
    item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
}
function preserve(player, fields, fn) {
    const values = fields.map(field => player[field]);
    try { return fn(); }
    finally { fields.forEach((field, index) => { player[field] = values[index]; }); }
}
function count(probe, name) { return probe.counts[name] || 0; }

function localTests(player) {
    const self = Probe.get(player), other = Probe.get(Main.player[remote]);
    const sword = sample(Terraria.ID.ItemID.CopperShortsword), potion = sample(Terraria.ID.ItemID.LesserHealingPotion);
    check('instancias e objetos separados', () => self !== other && self.counts !== other.counts && self.payload.bag !== other.payload.bag);
    check('critico por jogador', () => {
        const before = player['int GetWeaponCrit(Item sItem)'](sword), remoteBefore = Main.player[remote]['int GetWeaponCrit(Item sItem)'](sword);
        self.crit = 12;
        try { return player['int GetWeaponCrit(Item sItem)'](sword) === before + 12 && Main.player[remote]['int GetWeaponCrit(Item sItem)'](sword) === remoteBefore; }
        finally { self.crit = 0; }
    });
    check('knockback por jogador', () => {
        const before = player['float GetWeaponKnockback(Item sItem, float KnockBack)'](sword, 4);
        self.knockback = 2;
        try { return Math.abs(player['float GetWeaponKnockback(Item sItem, float KnockBack)'](sword, 4) - before * 2) < 0.001 && other.knockback === 1; }
        finally { self.knockback = 1; }
    });
    check('escala por referencia', () => {
        const before = new Ref(1); player['void ApplyMeleeScale(ref float scale)'](before);
        self.scale = 2;
        try { const after = new Ref(1); player['void ApplyMeleeScale(ref float scale)'](after); return Math.abs(after.value - before.value * 2) < 0.001; }
        finally { self.scale = 1; }
    });
    check('multiplicadores de tempo e velocidade', () => preserve(player, ['itemTime', 'itemTimeMax'], () => {
        player['void ApplyItemTime(Item sItem)'](sword); const before = player.itemTime;
        self.speed = 2; self.time = 3;
        try { player['void ApplyItemTime(Item sItem)'](sword); return player.itemTime === Math.max(1, Math.trunc(before * 1.5)); }
        finally { self.speed = self.time = 1; }
    }));
    check('multiplicador de animacao', () => preserve(player, ['itemAnimation', 'itemAnimationMax'], () => {
        player['void ApplyItemAnimation(Item sItem)'](sword); const before = player.itemAnimation;
        self.animation = 2;
        try { player['void ApplyItemAnimation(Item sItem)'](sword); return player.itemAnimation === before * 2; }
        finally { self.animation = 1; }
    }));
    check('mana modificada e custo restaurado', () => preserve(player, ['statMana', 'manaCost', 'slowMagicUse'], () => {
        player.statMana = 100; player.manaCost = 1; self.mana = 0.5;
        const before = count(self, 'OnConsumeMana');
        try { player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](20, true, true); return player.statMana === 90 && player.manaCost === 1 && count(self, 'OnConsumeMana') === before + 1; }
        finally { self.mana = 1; }
    }));
    check('mana insuficiente nao notifica consumo', () => preserve(player, ['statMana', 'manaCost', 'manaFlower', 'slowMagicUse'], () => {
        player.statMana = 0; player.manaCost = 1; player.manaFlower = false;
        const before = count(self, 'OnMissingMana'), consumed = count(self, 'OnConsumeMana');
        player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](20, true, false);
        return player.statMana === 0 && player.slowMagicUse && count(self, 'OnMissingMana') === before + 1 && count(self, 'OnConsumeMana') === consumed;
    }));
    check('blockQuickMana bloqueia OnMissingMana', () => preserve(player, ['statMana', 'manaCost', 'slowMagicUse'], () => {
        player.statMana = 0; player.manaCost = 1; const before = count(self, 'OnMissingMana');
        player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](20, true, true);
        return player.statMana === 0 && player.slowMagicUse && count(self, 'OnMissingMana') === before;
    }));
    check('mana parcial informa somente saldo consumido', () => preserve(player, ['statMana', 'manaCost', 'slowMagicUse', 'manaFlower'], () => {
        player.statMana = 6; player.manaCost = 1; player.manaFlower = false; const before = count(self, 'OnConsumeMana');
        player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](20, true, true);
        return player.statMana === 0 && player.slowMagicUse && count(self, 'OnConsumeMana') === before + 1 && self.lastConsumption === 6;
    }));
    check('veto de ItemCheck e PostItemCheck unico', () => {
        const before = count(self, 'PostItemCheck'), remoteBefore = count(other, 'PostItemCheck'); self.blockItem = true;
        try { player['void ItemCheck()'](); return count(self, 'PostItemCheck') === before + 1 && count(other, 'PostItemCheck') === remoteBefore; }
        finally { self.blockItem = false; }
    });
    check('veto de pocao', () => preserve(player, ['potionDelay'], () => {
        const before = player.potionDelay, calls = count(self, 'ApplyPotionDelay'); self.blockPotion = true;
        try { player['void ApplyPotionDelay(Item sItem)'](potion); return player.potionDelay === before && count(self, 'ApplyPotionDelay') === calls + 1; }
        finally { self.blockPotion = false; }
    }));
    check('cura chama hooks e restaura item', () => preserve(player, ['statLife', 'statMana'], () => {
        const beforeLife = count(self, 'GetHealLife'), beforeMana = count(self, 'GetHealMana'), life = potion.healLife;
        player['void ApplyLifeAndOrMana(Item item)'](potion);
        return count(self, 'GetHealLife') === beforeLife + 1 && count(self, 'GetHealMana') === beforeMana + 1 && potion.healLife === life;
    }));
    check('pesca por referencia', () => {
        const before = player['PlayerFishingConditions GetFishingConditions()']().FinalFishingLevel; self.fishing = 15;
        try { return player['PlayerFishingConditions GetFishingConditions()']().FinalFishingLevel === before + 15; }
        finally { self.fishing = 0; }
    });
    check('clone profundo sem compartilhar instancia', () => {
        const copy = Probe.get(player['Player clientClone()']());
        copy.payload.bag.value++;
        return copy !== self && copy.counts !== self.counts && copy.payload.bag !== self.payload.bag && copy.payload.bag.value === self.payload.bag.value + 1;
    });
    check('teleporte vetado sem mudar posicao', () => {
        const x = player.position.X, y = player.position.Y; self.blockTeleport = true;
        try { player['void Teleport(Vector2 newPos, int Style, int extraInfo)'](Vector2.new(x + 100, y), 0, 0); return player.position.X === x && player.position.Y === y; }
        finally { self.blockTeleport = false; }
    });
    check('visuais de salto vetados e estado restaurado', () => preserve(player, ['isPerformingJump_Cloud'], () => {
        player.isPerformingJump_Cloud = true; self.blockJumpVisuals = true; const before = count(self, 'ExtraJumpVisuals');
        try { player['void DoubleJumpVisuals()'](); return player.isPerformingJump_Cloud && count(self, 'ExtraJumpVisuals') === before; }
        finally { self.blockJumpVisuals = false; }
    }));
    check('visuais de salto autorizados', () => preserve(player, ['isPerformingJump_Cloud'], () => {
        player.isPerformingJump_Cloud = true; const before = count(self, 'ExtraJumpVisuals');
        player['void DoubleJumpVisuals()'](); return count(self, 'ExtraJumpVisuals') > before;
    }));
    check('refresh de salto', () => preserve(player, ['hasJumpOption_Cloud', 'canJumpAgain_Cloud'], () => {
        player.hasJumpOption_Cloud = true; const before = count(self, 'OnExtraJumpRefreshed');
        player['void RefreshDoubleJumps()'](); return player.canJumpAgain_Cloud && count(self, 'OnExtraJumpRefreshed') > before;
    }));
    check('fim de salto', () => preserve(player, ['isPerformingJump_Cloud'], () => {
        player.isPerformingJump_Cloud = true; const before = count(self, 'OnExtraJumpEnded');
        player['void CancelAllJumpVisualEffects(bool includeDownDash)'](false); return !player.isPerformingJump_Cloud && count(self, 'OnExtraJumpEnded') > before;
    }));
    check('veto e inicio de salto extra', () => preserve(player, ['velocity', 'jump', 'controlJump', 'releaseJump', 'jumpHeight', 'jumpSpeed',
        'canJumpAgain_Cloud', 'hasJumpOption_Cloud', 'isPerformingJump_Cloud'], () => {
        player.velocity = Vector2.new(0, 1); player.jump = 0; player.controlJump = player.releaseJump = true;
        player.hasJumpOption_Cloud = player.canJumpAgain_Cloud = true; player.isPerformingJump_Cloud = false;
        const before = count(self, 'OnExtraJumpStarted'); self.blockJump = true;
        try {
            player['void JumpMovement()']();
            if (player.isPerformingJump_Cloud || !player.canJumpAgain_Cloud || count(self, 'OnExtraJumpStarted') !== before) return false;
            self.blockJump = false; player.controlJump = player.releaseJump = true; player.velocity = Vector2.new(0, 1); player.jump = 0;
            player['void JumpMovement()']();
            return player.isPerformingJump_Cloud && count(self, 'OnExtraJumpStarted') === before + 1 && count(self, 'ModifyExtraJumpDurationMultiplier') > 0;
        } finally { self.blockJump = false; }
    }));
    check('bonus de armadura ativado e mantido', () => {
        const activated = count(self, 'ArmorSetBonusActivated'), held = count(self, 'ArmorSetBonusHeld');
        const direction = Main.ReversedUpDownArmorSetBonuses ? 1 : 0;
        player['void KeyDoubleTap(int keyDir)'](direction); player['void KeyHoldDown(int keyDir, int holdTime)'](direction, 15);
        return count(self, 'ArmorSetBonusActivated') === activated + 1 && count(self, 'ArmorSetBonusHeld') === held + 1;
    });
    check('ConsumableDodge impede dano local', () => {
        const before = player.statLife, calls = count(self, 'ConsumableDodge'); self.dodge = true;
        try {
            const reason = Terraria.DataStructures.PlayerDeathReason['PlayerDeathReason ByCustomReason(string reasonInEnglish)']('mpmodplayer');
            const result = player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'](reason, 1, 0, false, true, false, -1, true);
            return result === 0 && player.statLife === before && count(self, 'ConsumableDodge') === calls + 1;
        } finally { self.dodge = false; }
    });
    preserve(player, ['ammoCost75', 'ammoCost80', 'ammoBox', 'ammoPotion', 'itemTime', 'itemTimeMax', 'itemAnimation', 'itemAnimationMax'], () => {
        const inventory = player.inventory, previous = inventory[54], bow = sample(Terraria.ID.ItemID.WoodenBow), ammo = sample(Terraria.ID.ItemID.WoodenArrow);
        player.ammoCost75 = player.ammoCost80 = player.ammoBox = player.ammoPotion = false;
        inventory[54] = ammo; ammo.stack = 3;
        const pickAmmo = () => player['void PickAmmo(Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)'](
            bow, new Ref(bow.shoot), new Ref(bow.shootSpeed), new Ref(false), new Ref(10), new Ref(0), new Ref(0), false);
        try {
            check('veto de consumo de municao', () => {
                self.blockAmmo = true; const before = count(self, 'OnConsumeAmmo');
                try { pickAmmo(); return ammo.stack === 3 && ammo.consumable && count(self, 'CanConsumeAmmo') > 0 && count(self, 'OnConsumeAmmo') === before; }
                finally { self.blockAmmo = false; }
            });
            check('consumo de municao notifica uma vez', () => {
                const before = count(self, 'OnConsumeAmmo'); pickAmmo();
                return ammo.stack === 2 && count(self, 'OnConsumeAmmo') === before + 1;
            });
            check('CanShoot veta disparo', () => {
                const before = count(self, 'Shoot'); self.blockShoot = true;
                try { player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](player.whoAmI, bow, 10, false); return count(self, 'Shoot') === before && player.itemTime > 0; }
                finally { self.blockShoot = false; }
            });
            check('ModifyShootStats e Shoot compartilham estatisticas', () => {
                const before = count(self, 'Shoot'); self.blockProjectile = true;
                player.itemAnimation = bow.useAnimation; player.itemTime = 0;
                try { player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](player.whoAmI, bow, 10, false); return count(self, 'Shoot') === before + 1 && self.lastShotDamage === 42; }
                finally { self.blockProjectile = false; }
            });
        } finally { inventory[54] = previous; }
    });
}

function combatTests(player) {
    const self = Probe.get(player), npc = Main.npc[target], sword = sample(Terraria.ID.ItemID.CopperShortsword);
    check('NPC do host replicado no cliente', () => npc.active && npc.type === Terraria.ID.NPCID.BlueSlime);
    npc.life = npc.lifeMax = 10000;
    check('veto de dano recebido de NPC', () => {
        const before = player.statLife, calls = count(self, 'CanBeHitByNPC'); self.blockIncoming = true;
        try {
            const reason = Terraria.DataStructures.PlayerDeathReason['PlayerDeathReason ByNPC(int index)'](target);
            const result = player['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'](reason, 1, 0, false, true, false, -1, false);
            return result === 0 && player.statLife === before && count(self, 'CanBeHitByNPC') === calls + 1;
        } finally { self.blockIncoming = false; }
    });
    check('ataque de item pertence ao cliente', () => {
        const before = count(self, 'OnHitNPCWithItem'); self.hitBonus = 20;
        try {
            npc.immune[player.whoAmI] = 0;
            player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](sword, npc.Hitbox, 20, 0, target);
            return count(self, 'OnHitNPCWithItem') === before + 1 && count(self, 'ModifyHitNPCWithItem') > 0 && count(self, 'CanMeleeAttackCollideWithNPC') > 0;
        } finally { self.hitBonus = 0; }
    });
    check('projétil dispara callbacks do dono', () => {
        const source = Terraria.DataStructures.EntitySource_DebugCommand.new(); source['void .ctor()']();
        const index = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'](
            source, npc.Center.X, npc.Center.Y, 0, 0, Terraria.ID.ProjectileID.WoodenArrowFriendly, 20, 0, player.whoAmI, 0, 0, 0, null);
        const projectile = Main.projectile[index], before = count(self, 'OnHitNPCWithProj');
        projectile.tileCollide = false; projectile.position = Vector2.new(npc.position.X, npc.position.Y); projectile.velocity = Vector2.new(0, -2);
        npc.immune[player.whoAmI] = 0;
        try {
            projectile['void EmitEnchantmentVisualsAt(Vector2 boxPosition, int boxWidth, int boxHeight)'](projectile.position, 2, 2);
            projectile['void Damage()']();
            return count(self, 'OnHitNPCWithProj') === before + 1 && count(self, 'ModifyHitNPCWithProj') > 0 && count(self, 'EmitEnchantmentVisualsAt') > 0;
        } finally { if (projectile.active) projectile['void Kill()'](); }
    });
}

function report(player) {
    const self = Probe.get(player), other = Probe.get(Main.player[remote]);
    for (const name of ['PreUpdateMovement', 'PostUpdateMiscEffects', 'PostUpdateRunSpeeds', 'NaturalLifeRegen', 'ResetInfoAccessories',
        'DrawEffects', 'ModifyDrawInfo', 'HideDrawLayers', 'ModifyDrawLayerOrdering', 'TransformDrawData', 'DrawPlayer', 'ModifyScreenPosition', 'ModifyZoom']) {
        check('callback local ' + name, () => count(self, name) > 0);
    }
    check('atualizacao do jogador remoto isolada', () => count(other, 'PreUpdate') > 0 && count(other, 'PostUpdateMiscEffects') > 0);
    check('camera restrita ao jogador local', () => count(other, 'ModifyScreenPosition') === 0 && count(other, 'ModifyZoom') === 0);
    if (role === 'cliente') {
        check('SyncPlayer entrega estado inicial do host', () => hostSync);
        check('SendClientChanges envia somente duas alteracoes', () => JSON.stringify(deltas) === '[[0,11],[11,12]]');
        check('estado confirma ida e volta pela rede', () => acknowledgements.some(a => a[0] === 11 && a[1] === 111 && a[2] === 256) && acknowledgements.some(a => a[0] === 12 && a[1] === 122 && a[2] === 256));
        check('clone automatico executa no cliente', () => count(self, 'CopyClientState') > 10 && count(self, 'SendClientChanges') > 10);
        packet('finished', [failures]);
    } else {
        check('estado do cliente chega com origem correta', () => received.filter(s => s.index === remote && s.from === remote).some(s => s.revision === 12 && s.value === 122));
        check('estado remoto nao altera o host', () => self.payload.revision === 7 && self.payload.bag.value === 700);
        check('callbacks de ataque nao duplicam no host', () => count(other, 'OnHitNPCWithItem') === 0 && count(other, 'OnHitNPCWithProj') === 0);
        check('conexao dispara PlayerConnect', () => events.some(e => e[0] === 'PlayerConnect' && e[1] === remote));
        check('sincronizacao executa no host', () => events.some(e => e[0] === 'SyncPlayer' && e[1] === Main.myPlayer));
        if (target >= 0 && Main.npc[target].active) { Main.npc[target].active = false; sendData(23, -1, -1, null, target, 0, 0, 0, 0, 0, 0); }
    }
    finished = true;
    bl.log('mpmodplayer ' + role + ' FIM falhas=' + failures + ' casos=' + cases + ' hooks=' + Object.keys(self.counts).length);
    if (role === 'host') bl.log('mpmodplayer host READY_DISCONNECT');
}

P['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (!Main.gameMenu && !diagnosed) {
        diagnosed = true;
        bl.log('mpmodplayer contexto index=' + index + ' who=' + player.whoAmI + ' local=' + Main.myPlayer + ' netMode=' + Main.netMode);
    }
    if (index !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    if (!role) {
        role = (Main.netMode & 2) ? 'host' : Main.netMode === 1 ? 'cliente' : '';
        if (!role) return;
        if (role === 'host') Probe.get(player).payload = { revision: 7, bag: { value: 700 } };
        bl.log('mpmodplayer ' + role + ' INICIO jogador=' + index + ' netMode=' + Main.netMode);
    }
    if (!player.dead) player.statLife = player.statLifeMax2;
    if (finished) return;
    if (role === 'cliente' && started < 0 && frames % 120 === 0) packet('hello', []);
    if (started < 0 || remote < 0) return;
    const elapsed = frames - started;
    if (elapsed === 30) {
        localTests(player);
        if (role === 'host') {
            const source = Terraria.DataStructures.EntitySource_DebugCommand.new(); source['void .ctor()']();
            const position = Main.player[remote].position;
            target = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'](
                source, Math.trunc(position.X + 100), Math.trunc(position.Y - 100), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
            const npc = Main.npc[target]; npc.life = npc.lifeMax = 10000; npc.damage = 0; npc.defense = 0; npc.noGravity = true; npc.noTileCollide = true; npc.aiStyle = -1;
            sendData(23, remote, -1, null, target, 0, 0, 0, 0, 0, 0); packet('target', [target], remote);
        }
    }
    if (role === 'cliente') {
        if (elapsed === 60) { const probe = Probe.get(player); probe.payload.revision = 11; probe.payload.bag.value = 111; }
        if (elapsed === 90) { const probe = Probe.get(player); probe.payload.revision = 12; probe.payload.bag.value = 122; }
        if (elapsed === 120) combatTests(player);
        if (elapsed === 240) report(player);
    } else if (clientFinished && elapsed > 260) report(player);
    if (elapsed > 1800 && !finished) { check('conclusao dentro do prazo', () => false); report(player); }
});
bl.log('mpmodplayer carregado');
