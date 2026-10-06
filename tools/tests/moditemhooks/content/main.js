const Main = Terraria.Main;
let done = false, frames = 0, checks = 0, failures = 0;
const state = {}, counts = {};
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
Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    if (++frames !== 60) return;
    done = true;
    try { run(player); }
    catch (error) { failures++; log('execucao: FALHOU ' + error + ' ' + error.stack); }
    log('FIM checks=' + checks + ' falhas=' + failures);
});
export default class ItemProbeTests extends Mod {}
