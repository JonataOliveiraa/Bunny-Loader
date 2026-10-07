const Main = Terraria.Main;
const observed = new Map();
const methods = ['ModifyWeaponDamage', 'ModifyWeaponCrit', 'ModifyWeaponKnockback', 'ModifyItemScale', 'CanHitNPC', 'ModifyHitNPC',
    'CanHitPvp', 'ModifyHitPvp', 'OnHitPvp', 'CanMeleeAttackCollideWithNPC', 'MeleeEffects', 'UseItemHitbox',
    'UseAnimation', 'UseItemFrame', 'HoldItemFrame', 'NeedsAmmo', 'CanChooseAmmo', 'CanBeChosenAsAmmo', 'CanConsumeAmmo',
    'CanBeConsumedAsAmmo', 'OnConsumeAmmo', 'OnConsumedAsAmmo', 'PickAmmo', 'GetHealLife', 'GetHealMana', 'ApplyPotionDelay', 'ModifyPotionDelay'];
let frameHookIndex = -1, pvpState = null, pvpWeapon = null;
const pvpGate = Terraria.Player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'];
pvpGate.hook((original, player, item, rectangle, damage, knockback) => original(player, item, rectangle, damage, knockback));
Terraria.NetMessage['void SendPlayerHurt(int playerTargetIndex, PlayerDeathReason reason, int damage, int direction, bool critical, bool pvp, int hitContext, int remoteClient, int ignoreClient)'].hook(
    (original, target, reason, damage, direction, critical, pvp, context, remoteClient, ignoreClient) => {
        bl.log('mpmodplayer PVP envio local=' + Main.myPlayer + ' alvo=' + target + ' origem=' + reason._sourcePlayerIndex + ' item=' + reason._sourceItemType + ' dano=' + damage + ' pvp=' + pvp);
        return original(target, reason, damage, direction, critical, pvp, context, remoteClient, ignoreClient);
    }, { whileIn: pvpGate });

function record(item, method, player) {
    item.calls[method] = (item.calls[method] || 0) + 1;
    let counts = observed.get(player.whoAmI);
    if (!counts) { counts = {}; observed.set(player.whoAmI, counts); }
    counts[method] = (counts[method] || 0) + 1;
}

class TestItem extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    setup(item) { item.width = item.height = 16; item.maxStack = 1; this.state = {}; this.calls = {}; }
}

export class MultiplayerMeleeItem extends TestItem {
    SetDefaults(item) {
        this.setup(item); item.damage = 20; item.knockBack = 3; item.melee = true;
        item.useStyle = 1; item.useTime = item.useAnimation = 20; item.scale = 1;
    }
    ModifyWeaponDamage(item, player, damage) { record(this, 'ModifyWeaponDamage', player); damage.Flat += this.state.damage || 0; }
    ModifyWeaponCrit(item, player, value) { record(this, 'ModifyWeaponCrit', player); value.value += this.state.crit || 0; }
    ModifyWeaponKnockback(item, player, value) { record(this, 'ModifyWeaponKnockback', player); value.Multiplicative *= this.state.knockback || 1; }
    ModifyItemScale(item, player, value) { record(this, 'ModifyItemScale', player); value.value *= this.state.scale || 1; }
    UseItemHitbox(item, player, hitbox, noHitbox) {
        record(this, 'UseItemHitbox', player);
        if (this.state.hitbox) hitbox.value = this.state.hitbox;
        if (this.state.noHitbox) noHitbox.value = true;
    }
    MeleeEffects(item, player, hitbox) { record(this, 'MeleeEffects', player); this.lastHitbox = hitbox; }
    CanHitNPC(item, player) { record(this, 'CanHitNPC', player); return this.state.canHit ?? null; }
    CanMeleeAttackCollideWithNPC(item, player) { record(this, 'CanMeleeAttackCollideWithNPC', player); return this.state.collision ?? null; }
    ModifyHitNPC(item, player, target, modifiers) { record(this, 'ModifyHitNPC', player); if (this.state.hitDamage) { modifiers.damage = this.state.hitDamage; modifiers.DisableCrit(); } }
    OnHitNPC(item, player, target, hit, damageDone) { record(this, 'OnHitNPC', player); this.lastDamage = damageDone; }
    CanHitPvp(item, player) { record(this, 'CanHitPvp', player); return this.state.pvpCan !== false; }
    ModifyHitPvp(item, player, target, modifiers) { record(this, 'ModifyHitPvp', player); modifiers.damage = 3; modifiers.crit = false; }
    OnHitPvp(item, player, target, info) { record(this, 'OnHitPvp', player); this.pvpInfo = info; }
}

export class MultiplayerRangedItem extends TestItem {
    Load() { frameHookIndex = bl.hookStats().filter(s => s.name.includes('PlayerFrame(')).length; }
    SetDefaults(item) {
        this.setup(item); item.damage = 20; item.ranged = true; item.useStyle = 5;
        item.useTime = item.useAnimation = 20; item.shoot = 1; item.shootSpeed = 5; item.useAmmo = Terraria.ID.AmmoID.Arrow;
    }
    UseAnimation(item, player) { record(this, 'UseAnimation', player); if (this.state.animation) item.useAnimation = this.state.animation; }
    UseItemFrame(item, player) { record(this, 'UseItemFrame', player); if (this.state.frame) player.bodyFrame = Rectangle.new(0, this.state.frame, 40, 56); }
    HoldItemFrame(item, player) { record(this, 'HoldItemFrame', player); if (this.state.frame) player.bodyFrame = Rectangle.new(0, this.state.frame, 40, 56); }
    NeedsAmmo(item, player) { record(this, 'NeedsAmmo', player); return this.state.needs !== false; }
    CanChooseAmmo(item, ammo, player) { record(this, 'CanChooseAmmo', player); return ammo.type === this.state.candidateType ? this.state.choose ?? null : null; }
    CanConsumeAmmo(item, ammo, player) { record(this, 'CanConsumeAmmo', player); return this.state.consume !== false; }
    OnConsumeAmmo(item, ammo, player) { record(this, 'OnConsumeAmmo', player); this.consumed = { type: ammo.type, stack: ammo.stack }; }
}

export class MultiplayerAmmoItem extends TestItem {
    SetDefaults(item) {
        this.setup(item); item.damage = 4; item.ranged = true; item.ammo = Terraria.ID.AmmoID.Arrow;
        item.shoot = 1; item.shootSpeed = 2; item.knockBack = 1; item.consumable = true; item.maxStack = 9999;
    }
    CanBeChosenAsAmmo(item, weapon, player) { record(this, 'CanBeChosenAsAmmo', player); return this.state.chosen ?? null; }
    CanBeConsumedAsAmmo(item, weapon, player) { record(this, 'CanBeConsumedAsAmmo', player); return this.state.consume !== false; }
    PickAmmo(item, weapon, player, type, speed, damage, knockback) {
        record(this, 'PickAmmo', player);
        if (this.state.modify) { type.value = 2; speed.value = 12; damage.Flat += 7; knockback.value = 9; }
    }
    OnConsumedAsAmmo(item, weapon, player) { record(this, 'OnConsumedAsAmmo', player); this.consumedType = item.type; }
}

export class MultiplayerHealingItem extends TestItem {
    SetDefaults(item) {
        this.setup(item); item.useStyle = 2; item.useTime = item.useAnimation = 17;
        item.healLife = 50; item.healMana = 20; item.potion = item.consumable = true; item.maxStack = 9999;
    }
    GetHealLife(item, player, quick, value) { record(this, 'GetHealLife', player); this.lifeQuick = quick; if (this.state.life !== undefined) value.value = this.state.life; }
    GetHealMana(item, player, quick, value) { record(this, 'GetHealMana', player); this.manaQuick = quick; if (this.state.mana !== undefined) value.value = this.state.mana; }
    ModifyPotionDelay(item, player, value) { record(this, 'ModifyPotionDelay', player); if (this.state.delay !== undefined) value.value = this.state.delay; }
    ApplyPotionDelay(item, player, delay) { record(this, 'ApplyPotionDelay', player); this.delaySeen = delay; return this.state.allow !== false; }
}

function sample(type) {
    const item = Terraria.Item.new(); item['void .ctor()'](); item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
}
function snapshot(entity, fields) { return fields.map(key => [key, entity[key]]); }
function restore(entity, values) { for (const [key, value] of values) entity[key] = value; }
function inventoryScope(player, fields, callback) {
    const inventory = Array.from({ length: player.inventory.length }, (_, i) => player.inventory[i]);
    const values = snapshot(player, fields), selected = player.selectedItemState.selected;
    try { return callback(); }
    finally {
        for (let i = 0; i < inventory.length; i++) player.inventory[i] = inventory[i];
        player.selectedItemState.selected = selected; restore(player, values);
    }
}

export function itemLocalTests(player, remote, check, probe) {
    const weapon = sample(ModContent.ItemType(MultiplayerMeleeItem)), other = sample(weapon.type), self = weapon.ModItem;
    const vanilla = sample(Terraria.ID.ItemID.CopperShortsword);
    check('ModItem instancias e estados separados', () => self !== other.ModItem && self.state !== other.ModItem.state && self.calls !== other.ModItem.calls);
    inventoryScope(player, ['meleeScaleGlove', 'itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax'], () => {
        player.inventory[player.selectedItem] = weapon;
        const damage = player['int GetWeaponDamage(Item sItem)'], crit = player['int GetWeaponCrit(Item sItem)'];
        const knockback = player['float GetWeaponKnockback(Item sItem, float KnockBack)'];
        const scale = () => player['float GetAdjustedItemScale(Item item)'](weapon);
        const baseDamage = damage(weapon), baseCrit = crit(weapon), baseKnockback = knockback(weapon, 3), baseScale = scale();
        self.state.damage = 7; self.state.crit = 11; self.state.knockback = 2; self.state.scale = 1.5;
        check('ModItem ModifyWeaponDamage muda valor nativo', () => damage(weapon) === baseDamage + 7);
        check('ModItem ModifyWeaponCrit muda valor nativo', () => crit(weapon) === baseCrit + 11);
        check('ModItem ModifyWeaponKnockback muda valor nativo', () => Math.abs(knockback(weapon, 3) - baseKnockback * 2) < .001);
        check('ModItem ModifyItemScale muda valor e preserva item', () => Math.abs(scale() - baseScale * 1.5) < .001 && weapon.scale === 1);
        const frame = Rectangle.new(0, 0, 16, 16), box = new Ref(frame), noHitbox = new Ref(false);
        self.state.hitbox = Rectangle.new(11, 22, 33, 44); self.state.noHitbox = true;
        player['void ItemCheck_GetMeleeHitbox(Item sItem, Rectangle heldItemFrame, out bool dontAttack, out Rectangle itemRectangle)'](weapon, frame, noHitbox, box);
        check('ModItem UseItemHitbox substitui retangulo e veto', () => box.value.X === 11 && box.value.Width === 33 && box.value.Height === 44 && noHitbox.value);
        player['void ItemCheck_EmitUseVisuals(Item sItem, Rectangle itemRectangle)'](weapon, frame);
        check('ModItem MeleeEffects recebe retangulo nativo', () => self.calls.MeleeEffects === 1 && self.lastHitbox.Width === 16);
        const before = JSON.stringify(self.calls); damage(vanilla); crit(vanilla); knockback(vanilla, 3);
        check('ModItem consultas vanilla nao executam callbacks do item', () => JSON.stringify(self.calls) === before);
        check('ModItem alteracoes nao vazam para outra instancia', () => damage(other) === baseDamage && !other.ModItem.state.damage);
    });
    ammoTests(player, check, probe);
    healingTests(player, check, probe);
    check('ModItem jogador remoto conserva modificadores', () => remote !== player.whoAmI && Main.player[remote].active);
}

function ammoTests(player, check, probe) {
    const weapon = sample(ModContent.ItemType(MultiplayerRangedItem)), ammo = sample(ModContent.ItemType(MultiplayerAmmoItem));
    const w = weapon.ModItem, ammoType = ammo.type;
    inventoryScope(player, ['ammoCyclingMode', 'ammoCyclingOffset', 'ammoCost75', 'ammoCost80', 'ammoBox', 'ammoPotion', 'magicQuiver',
        'itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax', 'reuseDelay', 'bodyFrame', 'legFrame', 'bodyFrameCounter', 'legFrameCounter'], () => {
        const reset = stack => {
            w.state = { candidateType: ammoType }; ammo['void SetDefaults(int Type, ItemVariant variant)'](ammoType, null); ammo.stack = stack ?? 5;
            for (let i = 0; i < player.inventory.length; i++) player.inventory[i] = sample(0);
            player.inventory[0] = weapon; player.inventory[54] = ammo; player.selectedItemState.selected = 0;
            player.ammoCyclingMode = player.ammoCyclingOffset = 0;
            player.ammoCost75 = player.ammoCost80 = player.ammoBox = player.ammoPotion = player.magicQuiver = false;
        };
        const choose = () => player['Item PickAmmo_PickAmmoItem(Item sItem)'](weapon);
        const shoot = dontConsume => {
            const type = new Ref(weapon.shoot), speed = new Ref(weapon.shootSpeed), allowed = new Ref(false);
            const damage = new Ref(20), knockback = new Ref(3), used = new Ref(0);
            player['void PickAmmo(Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)'](
                weapon, type, speed, allowed, damage, knockback, used, !!dontConsume);
            return { type: type.value, speed: speed.value, allowed: allowed.value, damage: damage.value, knockback: knockback.value, used: used.value };
        };
        reset(); w.state.animation = 31; player['void ApplyItemAnimation(Item sItem)'](weapon);
        check('ModItem UseAnimation precede calculo nativo', () => w.calls.UseAnimation === 1 && weapon.useAnimation === 31 && player.itemAnimation > 0);
        weapon.useAnimation = 20; w.state.frame = 336; player.itemAnimation = player.itemAnimationMax = 10;
        player['void PlayerFrame()']();
        check('ModItem UseItemFrame altera frame durante uso', () => w.calls.UseItemFrame === 1 && player.bodyFrame.Y === 336);
        player.itemAnimation = 0; player['void PlayerFrame()']();
        check('ModItem HoldItemFrame altera frame ocioso', () => w.calls.HoldItemFrame === 1 && player.bodyFrame.Y === 336);
        player.inventory[0] = sample(Terraria.ID.ItemID.WoodenBow);
        const entries = () => bl.hookStats().filter(s => s.name.includes('PlayerFrame('))[frameHookIndex].js;
        const beforeFrames = entries(); for (let i = 0; i < 30; i++) player['void PlayerFrame()']();
        check('ModItem PlayerFrame vanilla tem zero entradas no hook do item', () => entries() === beforeFrames);
        reset(); w.state.choose = false;
        check('ModItem CanChooseAmmo false recusa candidato', () => !choose());
        reset(); ammo.ammo = Terraria.ID.AmmoID.Bullet; w.state.choose = true;
        check('ModItem CanChooseAmmo true permite outra categoria', () => bl.addressOf(choose()) === bl.addressOf(ammo));
        ammo.ModItem.state.chosen = false;
        check('ModItem CanBeChosenAsAmmo veta permissao da arma', () => !choose());
        reset(); player.inventory[54] = sample(0); w.state.needs = false;
        check('ModItem NeedsAmmo false fornece municao virtual', () => player['bool HasAmmo(Item sItem, bool canUse)'](weapon, true) && shoot(true).allowed);
        reset(); const base = shoot(true); ammo.ModItem.state.modify = true; const modified = shoot(true);
        check('ModItem PickAmmo altera estatisticas finais', () => modified.type === 2 && modified.speed === 12 && modified.damage === base.damage + 7 && modified.knockback === 9);
        check('ModItem dontConsume preserva stack e notificacoes', () => ammo.stack === 5 && !w.calls.OnConsumeAmmo);
        for (const owner of ['arma', 'municao', 'ModPlayer']) {
            reset(1);
            if (owner === 'arma') w.state.consume = false;
            if (owner === 'municao') ammo.ModItem.state.consume = false;
            if (owner === 'ModPlayer') probe.blockAmmo = true;
            const before = w.calls.OnConsumeAmmo || 0;
            try { check('ModItem veto de consumo ' + owner + ' preserva ultima unidade', () => shoot().allowed && ammo.stack === 1 && ammo.consumable && (w.calls.OnConsumeAmmo || 0) === before); }
            finally { probe.blockAmmo = false; }
        }
        reset(1); const ammoInstance = ammo.ModItem, beforeWeapon = w.calls.OnConsumeAmmo || 0, beforePlayer = probe.counts.OnConsumeAmmo || 0;
        const last = shoot();
        check('ModItem ultima municao notifica arma municao e jogador uma vez', () => last.allowed && w.calls.OnConsumeAmmo === beforeWeapon + 1 && ammoInstance.calls.OnConsumedAsAmmo === 1 && probe.counts.OnConsumeAmmo === beforePlayer + 1);
        check('ModItem ultima municao conserva tipo antes de TurnToAir', () => w.consumed.type === ammoInstance.Type && w.consumed.stack === 0 && ammoInstance.consumedType === ammoInstance.Type && ammo.type === 0 && last.used === ammoInstance.Type);
    });
}

function healingTests(player, check, probe) {
    const potion = sample(ModContent.ItemType(MultiplayerHealingItem)), self = potion.ModItem;
    const buffs = Array.from({ length: player.buffType.length }, (_, i) => [player.buffType[i], player.buffTime[i]]);
    try {
        inventoryScope(player, ['statLife', 'statLifeMax2', 'statMana', 'statManaMax2', 'potionDelay', 'manaPotionDelay', 'potionDelayTime',
            'cursed', 'frozen', 'stoned', 'webbed', 'dead', 'itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax', 'reuseDelay'], () => {
            const reset = () => {
                self.state = {}; self.calls = {}; potion.stack = 5;
                player.statLifeMax2 = 500; player.statLife = 100; player.statManaMax2 = 200; player.statMana = 20;
                player.potionDelay = player.manaPotionDelay = 0; player.potionDelayTime = 3600;
                player.itemAnimation = player.itemTime = player.reuseDelay = 0;
                player.cursed = player.frozen = player.stoned = player.webbed = player.dead = false;
                for (let i = 0; i < player.inventory.length; i++) player.inventory[i] = sample(0);
                for (let i = 0; i < player.buffType.length; i++) { player.buffType[i] = 0; player.buffTime[i] = 0; }
                player.inventory[0] = potion;
            };
            const apply = player['void ApplyLifeAndOrMana(Item item)'], delay = player['void ApplyPotionDelay(Item sItem)'];
            const buff = type => { for (let i = 0; i < player.buffType.length; i++) if (player.buffType[i] === type) return player.buffTime[i]; return 0; };
            reset(); self.state.life = 80; self.state.mana = 35; apply(potion);
            check('ModItem GetHealLife e GetHealMana alteram recursos reais', () => player.statLife === 180 && player.statMana === 55 && self.calls.GetHealLife === 1 && self.calls.GetHealMana === 1 && !self.lifeQuick && !self.manaQuick);
            check('ModItem cura restaura campos e conserva ManaSickness', () => potion.healLife === 50 && potion.healMana === 20 && buff(94) > 0);
            reset(); self.state.delay = 1234; delay(potion);
            check('ModItem ModifyPotionDelay altera contador e buff', () => player.potionDelay === 1234 && buff(21) === 1234 && self.delaySeen === 1234);
            reset(); player.potionDelay = 7; self.state.allow = false; delay(potion);
            check('ModItem ApplyPotionDelay veta contador e buff', () => player.potionDelay === 7 && buff(21) === 0 && self.calls.ApplyPotionDelay === 1);
            reset(); self.state.life = 80; self.state.mana = 35; self.state.delay = 120; player['void QuickHeal()']();
            check('ModItem QuickHeal aplica cura consumo e atraso', () => player.statLife === 180 && player.statMana === 55 && potion.stack === 4 && player.potionDelay === 120 && buff(21) === 120 && self.lifeQuick && self.manaQuick);
            reset(); self.state.life = 65; self.state.mana = 45; self.state.allow = false; player['void QuickMana()']();
            check('ModItem QuickMana conserva cura e consumo com atraso vetado', () => player.statLife === 165 && player.statMana === 65 && potion.stack === 4 && player.potionDelay === 0 && buff(94) > 0 && self.lifeQuick && self.manaQuick);
            reset(); self.state.life = 80; self.state.mana = 35; probe.lifeBonus = 10; probe.manaBonus = 5;
            try { apply(potion); check('ModItem cura compoe com ModPlayer', () => player.statLife === 190 && player.statMana === 60); }
            finally { probe.lifeBonus = probe.manaBonus = 0; }
            reset(); self.state.delay = 240; probe.blockPotion = true;
            try { delay(potion); check('ModItem permissao nao supera veto de ModPlayer', () => self.delaySeen === 240 && player.potionDelay === 0 && buff(21) === 0); }
            finally { probe.blockPotion = false; }
        });
    } finally {
        for (let i = 0; i < buffs.length; i++) { player.buffType[i] = buffs[i][0]; player.buffTime[i] = buffs[i][1]; }
    }
}

export function itemCombatTests(player, target, check) {
    const weapon = sample(ModContent.ItemType(MultiplayerMeleeItem)), self = weapon.ModItem, npc = Main.npc[target];
    const attack = rect => { npc.immune[player.whoAmI] = 0; player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](weapon, rect || npc.Hitbox, 20, 0, target); };
    npc.life = npc.lifeMax = 10000; npc.defense = 0; self.state.canHit = false; let life = npc.life;
    attack(); check('ModItem CanHitNPC veta dano do NPC replicado', () => npc.life === life && self.calls.CanHitNPC > 0 && !self.calls.ModifyHitNPC);
    self.state.canHit = null; self.state.hitDamage = 5; attack();
    check('ModItem ModifyHitNPC altera dano real e OnHitNPC unico', () => npc.life === life - 5 && self.calls.ModifyHitNPC === 1 && self.calls.OnHitNPC === 1 && self.lastDamage === 5);
    self.state.collision = false; life = npc.life; attack();
    check('ModItem CanMeleeAttackCollideWithNPC veta dano', () => npc.life === life && self.calls.CanMeleeAttackCollideWithNPC > 0);
    const far = Rectangle.new(-10000, -10000, 10, 10); self.state.collision = null; attack(far);
    check('ModItem colisao null conserva regra nativa', () => npc.life === life);
    self.state.collision = true; attack(far);
    check('ModItem colisao true substitui intersecao nativa', () => npc.life === life - 5);
}

export function beginPvp(player, other) {
    if (pvpState) throw Error('sessao PvP duplicada');
    pvpState = [player, other].map(target => ({ player: target, values: snapshot(target, ['hostile', 'team', 'immune', 'immuneTime', 'statDefense']), cooldowns: [target.hurtCooldowns[0], target.hurtCooldowns[1]] }));
    maintainPvp();
}
export function maintainPvp() {
    if (!pvpState) return;
    for (const { player } of pvpState) { player.hostile = true; player.team = 0; player.immune = false; player.immuneTime = 0; player.statDefense = 0; player.hurtCooldowns[0] = player.hurtCooldowns[1] = 0; }
}
export function endPvp() {
    if (!pvpState) return;
    for (const { player, values, cooldowns } of pvpState) { restore(player, values); player.hurtCooldowns[0] = cooldowns[0]; player.hurtCooldowns[1] = cooldowns[1]; }
    pvpState = null;
}
export function itemPvpTests(player, other, check, phase) {
    if (phase === 'veto') pvpWeapon = sample(ModContent.ItemType(MultiplayerMeleeItem));
    if (!pvpWeapon) throw Error('arma PvP ausente');
    const weapon = pvpWeapon, self = weapon.ModItem;
    inventoryScope(player, ['itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax'], () => {
        player.inventory[player.selectedItem] = weapon;
        const attack = () => player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'](weapon, other.Hitbox, 10, 0);
        if (phase === 'veto') {
            maintainPvp(); self.state.pvpCan = false; const life = other.statLife; attack();
            check('ModItem CanHitPvp veta dano na copia do remoto', () => self.calls.CanHitPvp > 0 && !self.calls.ModifyHitPvp && !self.calls.OnHitPvp && other.statLife === life);
        } else {
            self.state.pvpCan = true; maintainPvp(); attack();
            check('ModItem ModifyHitPvp executa na copia do remoto', () => self.calls.ModifyHitPvp === 1);
            check('ModItem OnHitPvp notifica uma vez na copia do remoto', () => self.calls.OnHitPvp === 1 && self.pvpInfo && self.pvpInfo.Damage > 0);
        }
        bl.log('mpmodplayer PVP fase=' + phase + ' atacante=' + player.whoAmI + ' alvo=' + other.whoAmI + ' callbacks=' + JSON.stringify(self.calls) + ' dano=' + (self.pvpInfo && self.pvpInfo.Damage));
    });
}

export function checkIncomingPvp(attacker, check) {
    const counts = observed.get(attacker) || {};
    check('ModItem ModifyHitPvp executa no processo do alvo', () => counts.ModifyHitPvp === 1);
    check('ModItem OnHitPvp executa no processo do alvo', () => counts.OnHitPvp === 1);
    bl.log('mpmodplayer PVP alvo_local=' + Main.myPlayer + ' callbacks_atacante_remoto=' + JSON.stringify(counts));
}

export function checkItemCoverage(player, check) {
    const counts = observed.get(player.whoAmI) || {};
    for (const method of methods) check('ModItem callback ' + method, () => counts[method] > 0);
    bl.log('mpmodplayer ITEM jogador=' + player.whoAmI + ' cobertura=' + methods.filter(method => counts[method] > 0).length + '/27 ' + JSON.stringify(counts));
}
