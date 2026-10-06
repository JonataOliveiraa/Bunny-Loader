const Main = Terraria.Main;
let done = false, frames = 0, checks = 0, failures = 0;
const state = {}, counts = {};
const useState = {}, useCounts = {};
let frameHookIndex = -1;
function useRecord(name) { useCounts[name] = (useCounts[name] || 0) + 1; }
function log(text) { bl.log('moditemhooks ' + text); }
function record(name) { counts[name] = (counts[name] || 0) + 1; }
function check(name, test) {
    checks++;
    try { if (!test()) throw Error('resultado falso'); log(name + ': ok'); }
    catch (error) { failures++; log(name + ': FALHOU ' + error); }
}
export class ItemProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) {
        item.width = item.height = 16; item.damage = 20; item.knockBack = 3; item.crit = 0;
        item.melee = true; item.useStyle = 1; item.useTime = item.useAnimation = 20;
        item.maxStack = 1; item.scale = 1; item.noMelee = false;
    }
    ModifyWeaponDamage(item, player, damage) { record('damage'); if (state.damage) damage.Flat += state.damage; }
    ModifyWeaponCrit(item, player, crit) { record('crit'); if (state.crit) crit.value += state.crit; }
    ModifyWeaponKnockback(item, player, knockback) { record('knockback'); if (state.knockback) knockback.Multiplicative *= state.knockback; }
    ModifyItemScale(item, player, scale) { record('scale'); if (state.scale) scale.value *= state.scale; }
    UseItemHitbox(item, player, hitbox, noHitbox) {
        record('hitbox'); state.nativeBox = hitbox.value; state.nativeNoHitbox = noHitbox.value;
        if (state.hitbox) hitbox.value = state.hitbox;
        if (state.noHitbox) noHitbox.value = true;
    }
    CanHitNPC(item, player, target) { record('canHit'); return state.canHit; }
    CanMeleeAttackCollideWithNPC(item, player, hitbox, target) { record('collision'); return state.collision; }
    ModifyHitNPC(item, player, target, modifiers) {
        record('hit');
        if (state.hitDamage) { modifiers.damage = state.hitDamage; modifiers.DisableCrit(); }
    }
    OnHitNPC(item, player, target, damageDone, knockback, crit) { record('onHit'); state.damageDone = damageDone; }
    MeleeEffects(item, player, hitbox) { record('effects'); state.effectsBox = hitbox; }
    CanHitPvp(item, player, target) { record('pvpCan'); return state.pvpCan; }
    ModifyHitPvp(item, player, target, modifiers) { record('pvpHit'); if (state.pvpDamage) modifiers.damage = state.pvpDamage; }
    OnHitPvp(item, player, target, info) { record('pvpOn'); state.pvpInfo = info; }
}
export class ItemProbePlain extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) { item.width = item.height = 16; item.damage = 20; item.melee = true; item.scale = 1; }
}
export class ItemUseProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    Load() { frameHookIndex = bl.hookStats().filter(s => s.name.includes('PlayerFrame(')).length; }
    SetDefaults(item) {
        item.width = item.height = 16; item.damage = 20; item.ranged = true;
        item.useStyle = 5; item.useTime = item.useAnimation = 20; item.shoot = 1;
        item.shootSpeed = 5; item.useAmmo = Terraria.ID.AmmoID.Arrow; item.maxStack = 1;
    }
    UseAnimation(item, player) { useRecord('animation'); if (useState.animation) item.useAnimation = useState.animation; }
    UseItemFrame(item, player) { useRecord('useFrame'); if (useState.frame) player.bodyFrame = Rectangle.new(0, useState.frame, 40, 56); }
    HoldItemFrame(item, player) { useRecord('holdFrame'); if (useState.frame) player.bodyFrame = Rectangle.new(0, useState.frame, 40, 56); }
    NeedsAmmo(item, player) { useRecord('needs'); return useState.needs; }
    CanChooseAmmo(item, ammo, player) { useRecord('choose'); return ammo.type === useState.ammoType ? useState.choose : null; }
    CanConsumeAmmo(item, ammo, player) { useRecord('consume'); return useState.consume; }
    OnConsumeAmmo(item, ammo, player) { useRecord('onConsume'); useState.consumedType = ammo.type; useState.consumedStack = ammo.stack; }
}
export class ItemAmmoProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) {
        item.width = item.height = 16; item.damage = 4; item.ranged = true; item.ammo = Terraria.ID.AmmoID.Arrow;
        item.shoot = 1; item.shootSpeed = 2; item.knockBack = 1; item.consumable = true; item.maxStack = 9999;
    }
    CanBeChosenAsAmmo(item, weapon, player) { useRecord('chosen'); return useState.chosen; }
    CanBeConsumedAsAmmo(item, weapon, player) { useRecord('ammoConsume'); return useState.ammoConsume; }
    PickAmmo(item, weapon, player, type, speed, damage, knockback) {
        useRecord('pick');
        if (useState.modify) { type.value = 2; speed.value = 12; damage.Flat += 7; knockback.value = 9; }
    }
    OnConsumedAsAmmo(item, weapon, player) { useRecord('onConsumed'); useState.ammoConsumedType = item.type; }
}
export class ItemAmmoPlayerProbe extends ModPlayer {
    CanConsumeAmmo(player, weapon, ammo) { useRecord('mpConsume'); return useState.mpConsume; }
    OnConsumeAmmo(player, weapon, ammo) { useRecord('mpOnConsume'); useState.mpConsumedType = ammo.type; }
}
export class ItemProbeNPC extends ModNPC {
    Texture = 'Box';
    HideFromBestiary = true;
    HideFromModMenu = true;
    SetStaticDefaults() { Main.npcFrameCount[this.Type] = 1; }
    SetDefaults(npc) {
        npc.width = npc.height = 16; npc.lifeMax = 100000; npc.defense = npc.damage = 0;
        npc.aiStyle = -1; npc.noGravity = npc.noTileCollide = true; npc.npcSlots = 0;
    }
    CheckActive() { return false; }
    PreKill() { return false; }
}
function item(type) {
    const result = Terraria.Item.new(); result['void .ctor()']();
    result['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return result;
}
function snapshot(entity, fields) { return fields.map(key => [key, entity[key]]); }
function restore(entity, values) { for (const [key, value] of values) entity[key] = value; }
function run(player) {
    const weapon = item(ModContent.ItemType(ItemProbe)), plain = item(ModContent.ItemType(ItemProbePlain));
    const vanilla = item(Terraria.ID.ItemID.WoodenSword);
    const oldItem = player.inventory[player.selectedItem];
    const playerState = snapshot(player, ['hostile', 'team', 'meleeScaleGlove', 'statDefense', 'immune', 'immuneTime', 'itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax']);
    const victim = Main.player[player.whoAmI === 1 ? 2 : 1];
    const victimState = snapshot(victim, ['active', 'hostile', 'team', 'dead', 'statLife', 'statLifeMax2', 'statDefense', 'immune', 'immuneTime', 'position']);
    const cooldowns = [victim.hurtCooldowns[0], victim.hurtCooldowns[1]];
    let npc;
    try {
        check('singleplayer nativo', () => Main.netMode === 0);
        player.inventory[player.selectedItem] = weapon;
        const damage = player['int GetWeaponDamage(Item sItem)'];
        const crit = player['int GetWeaponCrit(Item sItem)'];
        const knockback = player['float GetWeaponKnockback(Item sItem, float KnockBack)'];
        const scale = player['float GetAdjustedItemScale(Item item)'];
        const baseDamage = damage(weapon); state.damage = 7;
        check('ModifyWeaponDamage altera resultado nativo', () => damage(weapon) === baseDamage + 7); state.damage = 0;
        const baseCrit = crit(weapon); state.crit = 13;
        check('ModifyWeaponCrit altera Ref nativo', () => crit(weapon) === baseCrit + 13); state.crit = 0;
        const baseKnockback = knockback(weapon, 3); state.knockback = 2;
        check('ModifyWeaponKnockback aplica StatModifier', () => Math.abs(knockback(weapon, 3) - baseKnockback * 2) < .001); state.knockback = 0;
        player.meleeScaleGlove = false; weapon.scale = 2; state.scale = 1.5;
        check('ModifyItemScale altera escala consultada', () => Math.abs(scale(weapon) - 3) < .001 && weapon.scale === 2);
        player.meleeScaleGlove = true;
        check('escala preserva luva do jogo', () => Math.abs(scale(weapon) - 3.3) < .001); weapon.scale = 1;
        const hitbox = player['void ItemCheck_GetMeleeHitbox(Item sItem, Rectangle heldItemFrame, out bool dontAttack, out Rectangle itemRectangle)'];
        const frame = Rectangle.new(0, 0, 16, 16), noHitbox = new Ref(false), box = new Ref(frame);
        player.meleeScaleGlove = false; state.scale = 0; hitbox(weapon, frame, noHitbox, box);
        const first = box.value;
        state.scale = 2; hitbox(weapon, frame, noHitbox, box);
        check('escala altera largura e altura da hitbox', () => box.value.Width === first.Width * 2 && box.value.Height === first.Height * 2);
        check('escala temporaria do item foi restaurada', () => weapon.scale === 1);
        state.hitbox = Rectangle.new(11, 22, 33, 44); state.noHitbox = true;
        hitbox(weapon, frame, noHitbox, box);
        check('UseItemHitbox troca Rectangle por Ref', () => box.value.X === 11 && box.value.Y === 22 && box.value.Width === 33 && box.value.Height === 44);
        check('UseItemHitbox altera dontAttack por Ref', () => noHitbox.value);
        state.hitbox = null; state.noHitbox = false; state.scale = 0;
        player['void ItemCheck_EmitUseVisuals(Item sItem, Rectangle itemRectangle)'](weapon, frame);
        check('MeleeEffects recebe hitbox apos codigo nativo', () => counts.effects === 1 && state.effectsBox.Width === 16);
        const before = JSON.stringify(counts);
        damage(plain); damage(vanilla); crit(plain); crit(vanilla); knockback(plain, 3); knockback(vanilla, 3);
        scale(plain); scale(vanilla); hitbox(plain, frame, noHitbox, box); hitbox(vanilla, frame, noHitbox, box);
        check('vanilla e mod sem sobrescritas nao executam callbacks', () => JSON.stringify(counts) === before);
        const source = Terraria.DataStructures.EntitySource_DebugCommand.new(); source['void .ctor()']();
        const c = player.Center;
        const index = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'](
            source, Math.floor(c.X + 80), Math.floor(c.Y - 60), ModContent.NPCType(ItemProbeNPC), 0, 0, 0, 0, 0, 255);
        if (index < 0 || index >= 200) throw Error('sem slot NPC'); npc = Main.npc[index];
        const attack = rect => {
            for (let i = 0; i < npc.immune.length; i++) npc.immune[i] = 0;
            player['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](weapon, rect || npc.Hitbox, 20, 0, npc.whoAmI);
        };
        state.canHit = false; let life = npc.life; const hitCalls = counts.hit || 0; attack();
        check('CanHitNPC false impede dano', () => npc.life === life && (counts.hit || 0) === hitCalls);
        state.canHit = null; state.hitDamage = 5; attack();
        check('ModifyHitNPC altera dano real do NPC', () => npc.life === life - 5 && counts.hit === hitCalls + 1);
        check('OnHitNPC existente continua uma vez com dano real', () => counts.onHit === 1 && state.damageDone === 5);
        state.collision = false; life = npc.life; attack();
        check('CanMeleeAttackCollideWithNPC false impede dano', () => npc.life === life);
        state.collision = null; const far = Rectangle.new(-10000, -10000, 10, 10); attack(far);
        check('colisao null conserva decisao nativa', () => npc.life === life);
        state.collision = true; attack(far);
        check('colisao true substitui Intersects nativo', () => npc.life === life - 5);
        const friendly = npc.friendly;
        try {
            npc.friendly = true; state.canHit = null; life = npc.life; attack();
            check('CanHitNPC null conserva recusa de NPC amigavel', () => npc.life === life);
            state.canHit = true; attack();
            check('CanHitNPC true permite NPC amigavel', () => npc.life === life - 5);
        } finally { npc.friendly = friendly; state.canHit = null; }
        state.collision = null;
        victim.active = victim.hostile = true; victim.dead = false; victim.team = 0;
        victim.statLife = victim.statLifeMax2 = 100000; victim.statDefense = 0;
        victim.position = Vector2.new(c.X + 64, c.Y); player.hostile = true; player.team = 0;
        const attackPvp = () => {
            victim.immune = false; victim.immuneTime = 0; victim.hurtCooldowns[0] = victim.hurtCooldowns[1] = 0;
            player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'](weapon, victim.Hitbox, 10, 0);
        };
        state.pvpCan = false; life = victim.statLife; attackPvp();
        check('CanHitPvp false impede Hurt real', () => victim.statLife === life && counts.pvpCan > 0 && !counts.pvpOn);
        state.pvpCan = true; state.pvpDamage = 3; attackPvp();
        check('ModifyHitPvp executa no Hurt real', () => counts.pvpHit === 1 && victim.statLife < life);
        check('OnHitPvp recebe HurtInfo do dano aplicado', () => counts.pvpOn === 1 && state.pvpInfo && state.pvpInfo.Damage === life - victim.statLife);
        log('PvP HurtInfo=' + (state.pvpInfo && state.pvpInfo.Damage) + ' deltaLife=' + (life - victim.statLife) + ' on=' + counts.pvpOn);
        const instances = weapon.ModItem;
        const samples = [];
        for (const [name, value] of [['vanilla', vanilla], ['plain', plain], ['override', weapon]]) {
            const times = [], callsBefore = counts.damage;
            const statsBefore = bl.hookStats().filter(s => s.name.includes('GetWeaponDamage('));
            for (let r = 0; r < 5; r++) {
                const start = performance.now(); for (let i = 0; i < 2000; i++) damage(value);
                times.push((performance.now() - start) * 1000 / 2000);
            }
            times.sort((a, b) => a - b); samples.push(name + '=' + times[2].toFixed(3));
            check('benchmark ' + name + ' contagem de callbacks', () => counts.damage === callsBefore + (name === 'override' ? 10000 : 0));
            const statsAfter = bl.hookStats().filter(s => s.name.includes('GetWeaponDamage('));
            const jsBefore = statsBefore.reduce((sum, s) => sum + s.js, 0);
            const jsAfter = statsAfter.reduce((sum, s) => sum + s.js, 0);
            check('filtro nativo ' + name + ' entradas JS', () => jsAfter - jsBefore === (name === 'override' ? 10000 : 0));
            log('despacho ' + name + ' hooks=' + statsAfter.length + ' js=' + (jsAfter - jsBefore));
        }
        check('instancia do item preservada no caminho frequente', () => weapon.ModItem === instances);
        log('benchmark GetWeaponDamage mediana_us ' + samples.join(' '));
    } finally {
        if (npc) npc.active = false;
        player.inventory[player.selectedItem] = oldItem;
        restore(player, playerState); restore(victim, victimState);
        victim.hurtCooldowns[0] = cooldowns[0]; victim.hurtCooldowns[1] = cooldowns[1];
    }
}
function runUse(player) {
    const weapon = item(ModContent.ItemType(ItemUseProbe)), ammo = item(ModContent.ItemType(ItemAmmoProbe));
    const plain = item(ModContent.ItemType(ItemProbePlain)), vanilla = item(Terraria.ID.ItemID.WoodenBow);
    const originalInventory = Array.from({ length: player.inventory.length }, (_, i) => player.inventory[i]);
    const originalState = snapshot(player, ['ammoCyclingMode', 'ammoCyclingOffset', 'ammoCost80', 'ammoCost75', 'ammoBox', 'ammoPotion', 'magicQuiver',
        'itemAnimation', 'itemAnimationMax', 'itemTime', 'itemTimeMax', 'reuseDelay', 'bodyFrame', 'legFrame', 'bodyFrameCounter', 'legFrameCounter']);
    const selectedState = player.selectedItemState, originalSelected = selectedState.selected;
    useState.ammoType = ammo.type;
    const ammoType = ammo.type;
    const reset = stack => {
        for (const key of Object.keys(useState)) delete useState[key]; useState.ammoType = ammoType;
        ammo['void SetDefaults(int Type, ItemVariant variant)'](ammoType, null); ammo.stack = stack === undefined ? 5 : stack;
        for (let i = 0; i < player.inventory.length; i++) player.inventory[i] = item(0);
        player.inventory[0] = weapon; player.inventory[54] = ammo; player.selectedItemState.selected = 0;
        player.ammoCyclingMode = 0; player.ammoCyclingOffset = 0;
        player.ammoCost80 = player.ammoCost75 = player.ammoBox = player.ammoPotion = player.magicQuiver = false;
    };
    const shoot = (dontConsume = false, chosenWeapon = weapon) => {
        const projectile = new Ref(chosenWeapon.shoot), speed = new Ref(chosenWeapon.shootSpeed), canShoot = new Ref(false);
        const damage = new Ref(20), knockback = new Ref(3), used = new Ref(0);
        player['void PickAmmo(Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)'](
            chosenWeapon, projectile, speed, canShoot, damage, knockback, used, dontConsume);
        return { projectile: projectile.value, speed: speed.value, canShoot: canShoot.value, damage: damage.value, knockback: knockback.value, used: used.value };
    };
    const choose = chosenWeapon => player['Item PickAmmo_PickAmmoItem(Item sItem)'](chosenWeapon || weapon);
    const hasAmmo = canUse => player['bool HasAmmo(Item sItem, bool canUse)'](weapon, canUse === undefined ? true : canUse);
    const frameStats = () => bl.hookStats().filter(s => s.name.includes('PlayerFrame('));
    const jsEntries = () => frameStats()[frameHookIndex].js;
    try {
        reset(); useState.animation = 31;
        player['void ApplyItemAnimation(Item sItem)'](weapon);
        check('UseAnimation altera duracao antes do calculo nativo', () => useCounts.animation === 1 && player.itemAnimation > 0 && weapon.useAnimation === 31);
        weapon.useAnimation = 20;
        reset(); player.itemAnimation = player.itemAnimationMax = 10; useState.frame = 336;
        player['void PlayerFrame()']();
        check('UseItemFrame altera bodyFrame apos o original', () => useCounts.useFrame === 1 && player.bodyFrame.Y === 336);
        player.itemAnimation = 0; player['void PlayerFrame()']();
        check('HoldItemFrame altera bodyFrame ocioso', () => useCounts.holdFrame === 1 && player.bodyFrame.Y === 336);
        for (const [name, value] of [['vanilla', vanilla], ['plain', plain]]) {
            player.inventory[0] = value; const before = jsEntries(), allBefore = frameStats().map(s => s.js);
            for (let i = 0; i < 100; i++) player['void PlayerFrame()']();
            check('PlayerFrame ModItem ' + name + ' tem zero entradas JS', () => jsEntries() === before);
            log('PlayerFrame ' + name + ' indice_ModItem=' + frameHookIndex + ' delta_js_por_hook=' + frameStats().map((s, i) => s.js - allBefore[i]).join(','));
        }
        reset(); useState.choose = false;
        check('CanChooseAmmo false retira candidato nativo', () => !choose() && !hasAmmo());
        const other = item(Terraria.ID.ItemID.WoodenArrow); player.inventory[55] = other;
        check('selecao avanca para proxima municao valida', () => bl.addressOf(choose()) === bl.addressOf(other));
        reset(); ammo.ammo = Terraria.ID.AmmoID.Bullet; useState.choose = true;
        check('CanChooseAmmo true permite outra categoria', () => bl.addressOf(choose()) === bl.addressOf(ammo) && hasAmmo());
        useState.chosen = false;
        check('veto da municao supera permissao da arma', () => !choose());
        reset(); ammo.ammo = Terraria.ID.AmmoID.Bullet; useState.chosen = true;
        check('CanBeChosenAsAmmo true funciona com arma vanilla', () => bl.addressOf(choose(vanilla)) === bl.addressOf(ammo));
        reset(); player.inventory[55] = other; player.ammoCyclingMode = 2; player.ammoCyclingOffset = 1;
        check('alternancia nativa segue ordem dos slots', () => bl.addressOf(choose()) === bl.addressOf(other));
        useState.choose = false;
        check('alternancia conta apenas candidatos aceitos', () => bl.addressOf(choose()) === bl.addressOf(other));
        reset(); player.inventory[54] = item(0);
        check('municao ausente conserva recusa nativa', () => !hasAmmo() && !shoot().canShoot);
        useState.needs = false; const virtual = shoot();
        check('NeedsAmmo false fornece municao padrao', () => hasAmmo() && virtual.canShoot && virtual.used === Terraria.ID.ItemID.WoodenArrow);
        check('NeedsAmmo false conserva veto canUse', () => !hasAmmo(false));
        reset(); const base = shoot(true); useState.modify = true; const modified = shoot(true);
        check('PickAmmo altera projetil e velocidade por Ref', () => modified.projectile === 2 && modified.speed === 12);
        check('PickAmmo altera dano e knockback finais', () => modified.damage === base.damage + 7 && modified.knockback === 9);
        check('dontConsume conserva stack e suprime notificacoes', () => ammo.stack === 5 && !useCounts.onConsume && !useCounts.onConsumed);
        for (const [name, key] of [['CanConsumeAmmo', 'consume'], ['CanBeConsumedAsAmmo', 'ammoConsume'], ['ModPlayer.CanConsumeAmmo', 'mpConsume']]) {
            reset(1); useState[key] = false; const before = useCounts.onConsume || 0; const shot = shoot();
            check(name + ' conserva ultima unidade e consumable', () => shot.canShoot && ammo.stack === 1 && ammo.consumable && (useCounts.onConsume || 0) === before);
        }
        reset(1); const before = useCounts.onConsume || 0, beforeAmmo = useCounts.onConsumed || 0, beforePlayer = useCounts.mpOnConsume || 0;
        const last = shoot();
        check('ultima unidade dispara tres notificacoes uma vez', () => last.canShoot && useCounts.onConsume === before + 1 && useCounts.onConsumed === beforeAmmo + 1 && useCounts.mpOnConsume === beforePlayer + 1);
        check('ultima unidade preserva tipos nos callbacks', () => useState.consumedType === ammoType && useState.ammoConsumedType === ammoType && useState.mpConsumedType === ammoType && useState.consumedStack === 0);
        check('ultima unidade executa TurnToAir nativo depois', () => ammo.type === 0 && ammo.stack === 0 && last.used === ammoType);
        reset(1); ammo.consumable = false; const notifications = useCounts.onConsume;
        shoot(); check('municao nao consumivel nao notifica', () => ammo.stack === 1 && useCounts.onConsume === notifications);
        const ammoHooks = bl.hookStats().filter(s => s.name.includes('PickAmmo('));
        check('ModItem e ModPlayer compartilham um hook PickAmmo', () => ammoHooks.length === 1);
        log('USE hooks_PickAmmo=' + ammoHooks.length + ' checks_total=' + checks);
    } finally {
        for (let i = 0; i < originalInventory.length; i++) player.inventory[i] = originalInventory[i];
        player.selectedItemState.selected = originalSelected;
        restore(player, originalState);
    }
}
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    if (++frames !== 60) return;
    done = true;
    try { run(player); runUse(player); }
    catch (error) { failures++; log('execucao: FALHOU ' + error + ' ' + error.stack); }
    log('FIM checks=' + checks + ' falhas=' + failures);
});
export default class ItemProbeTests extends Mod {}
