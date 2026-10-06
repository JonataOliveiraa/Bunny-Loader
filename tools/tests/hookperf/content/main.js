import { classCount } from './config.js';
const Main = Terraria.Main;
const log = value => bl.log('hookperf ' + JSON.stringify(value));
const ticks = ['ResetEffects', 'PreUpdate', 'PostUpdate', 'PreUpdateBuffs', 'PostUpdateBuffs', 'UpdateEquips',
    'PostUpdateEquips', 'UpdateBadLifeRegen', 'UpdateLifeRegen', 'PreUpdateMovement', 'PostUpdateMiscEffects',
    'PostUpdateRunSpeeds', 'NaturalLifeRegen', 'ResetInfoAccessories', 'PostItemCheck'];
let callbacks = 0, itemCallbacks = 0, phaseIndex = -1, warm = 0, collecting = false, done = false;
let before, updateTimes = [], drawTimes = [], phase, marker, drawingRegistered = false, helperMeasured = false;
const phases = classCount === 8 ? ['ticks_8', 'draw_empty', 'draw_helper', 'draw_hide', 'draw_reorder', 'draw_empty_repeat'] : ['ticks_' + classCount];

for (let i = 0; i < classCount; i++) {
    const Probe = class extends ModPlayer {};
    Object.defineProperty(Probe, 'name', { value: 'TickProbe' + i });
    for (const name of ticks) Probe.prototype[name] = function () { callbacks++; };
    Probe.prototype.PreItemCheck = function () { return false; };
    Probe.prototype.CanStartExtraJump = function () { return true; };
    ModPlayer.register(Probe);
}

export class PlainItem extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) {
        item.width = item.height = 16; item.damage = 20; item.knockBack = 3; item.melee = true;
        item.useStyle = 1; item.useTime = item.useAnimation = 20; item.maxStack = 1;
    }
}
export class ActiveItem extends PlainItem {
    ModifyWeaponDamage(item, player, damage) { itemCallbacks++; damage.Flat += 1; }
    ModifyWeaponCrit(item, player, value) { itemCallbacks++; value.value += 1; }
    ModifyWeaponKnockback() { itemCallbacks++; }
    ModifyItemScale() { itemCallbacks++; }
    UseItemFrame() { itemCallbacks++; }
    HoldItemFrame() { itemCallbacks++; }
}
export class AmmoWeapon extends PlainItem {
    SetDefaults(item) { super.SetDefaults(item); item.melee = false; item.ranged = true; item.useAmmo = Terraria.ID.AmmoID.Arrow; }
    CanChooseAmmo() { itemCallbacks++; return null; }
}

function sample(type) {
    const item = Terraria.Item.new(); item['void .ctor()'](); item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
}
function snapshot() { return bl.hookStats().map((row, index) => ({ ...row, index })); }
function entries(name) { return bl.hookStats().filter(row => row.name.includes(name + '(')).reduce((sum, row) => sum + row.js, 0); }
function measure(name, fn, count = 1000) {
    for (let i = 0; i < 100; i++) fn();
    const samples = [];
    for (let repeat = 0; repeat < 7; repeat++) {
        const start = performance.now();
        for (let i = 0; i < count; i++) fn();
        samples.push((performance.now() - start) * 1000 / count);
    }
    samples.sort((a, b) => a - b);
    log({ kind: 'micro', classes: classCount, phase, name, calls: 7 * count, medianUs: samples[3], minUs: samples[0], maxUs: samples[6] });
}
function summary(samples) {
    if (!samples.length) throw Error('nenhuma amostra');
    const sorted = samples.slice().sort((a, b) => a - b);
    return { count: samples.length, meanMs: samples.reduce((sum, value) => sum + value, 0) / samples.length,
        medianMs: sorted[Math.floor(sorted.length / 2)], p95Ms: sorted[Math.ceil(sorted.length * .95) - 1], maxMs: sorted[sorted.length - 1] };
}

class DrawProbe extends ModPlayer {
    ModifyDrawInfo(player, info) {
        if (Main.gameMenu || info.shadow !== 0 || player.whoAmI !== Main.myPlayer) return;
        if (!marker) {
            marker = Terraria.DataStructures.DrawData.new();
            marker['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
                Terraria.GameContent.TextureAssets.MagicPixel.Value, Vector2.new(Main.screenWidth / 2 + 70, Main.screenHeight / 2 - 30),
                Rectangle.new(0, 0, 1, 1), Color.new(255, 0, 255, 255), 0, Vector2.new(0, 0), 16, 0, 0);
        }
        if (!helperMeasured && phase === 'draw_empty') {
            helperMeasured = true;
            const count = info.DrawDataCacheCount;
            log({ kind: 'cache', capacity: info.DrawDataCache.length, activeBeforeLayers: count });
            measure('draw_manual', () => { const cache = info.DrawDataCache, count = info.DrawDataCacheCount; cache[count] = marker; info.DrawDataCacheCount = count + 1; info.DrawDataCacheCount = count; });
            measure('draw_helper', () => { const count = info.DrawDataCacheCount; if (!ModPlayer.AddDrawData(info, marker)) throw Error('cache valido recusado'); info.DrawDataCacheCount = count; });
        }
        if (phase === 'draw_helper' && !ModPlayer.AddDrawData(info, marker)) throw Error('cache valido recusado');
    }
    HideDrawLayers() { if (phase === 'draw_hide') PlayerDrawLayers.HeldItem.Hide(); }
    ModifyDrawLayerOrdering(player, positions) { if (phase === 'draw_reorder') positions.set(PlayerDrawLayers.Skin, PlayerDrawLayer.AfterParent(PlayerDrawLayers.Head)); }
}

function itemMeasurements(player) {
    const vanilla = sample(Terraria.ID.ItemID.CopperShortsword), plain = sample(ModContent.ItemType(PlainItem)), active = sample(ModContent.ItemType(ActiveItem));
    const damage = player['int GetWeaponDamage(Item sItem)'];
    for (const [name, item] of [['vanilla', vanilla], ['plain', plain], ['active', active]]) {
        const js = entries('GetWeaponDamage'), callbacksBefore = itemCallbacks;
        measure('weapon_damage_' + name, () => damage(item));
        log({ kind: 'filter', classes: classCount, name: 'weapon_damage_' + name, js: entries('GetWeaponDamage') - js, callbacks: itemCallbacks - callbacksBefore });
    }
    const inventory = Array.from({ length: player.inventory.length }, (_, index) => player.inventory[index]);
    const selected = player.selectedItemState.selected, body = player.bodyFrame, leg = player.legFrame;
    const weapon = sample(ModContent.ItemType(AmmoWeapon)), arrow = sample(Terraria.ID.ItemID.WoodenArrow);
    const cycling = player.ammoCyclingMode, animation = player.itemAnimation;
    try {
        player.itemAnimation = 0; player.ammoCyclingMode = 0;
        for (const [name, item] of [['vanilla', vanilla], ['plain', plain], ['active', active]]) {
            player.inventory[player.selectedItem] = item;
            const rows = snapshot(), count = itemCallbacks;
            measure('player_frame_' + name, () => player['void PlayerFrame()'](), 500);
            const after = snapshot();
            log({ kind: 'filter', classes: classCount, name: 'player_frame_' + name, callbacks: itemCallbacks - count,
                hooks: after.filter(row => row.name.includes('PlayerFrame(')).map(row => ({ index: row.index, js: row.js - rows[row.index].js })) });
        }
        for (let i = 0; i < player.inventory.length; i++) player.inventory[i] = sample(0);
        player.inventory[0] = weapon;
        const choose = () => player['Item PickAmmo_PickAmmoItem(Item sItem)'](weapon);
        measure('ammo_empty', choose, 250);
        player.inventory[54] = arrow; measure('ammo_first_slot', choose, 250);
        player.inventory[54] = sample(0); player.inventory[53] = arrow; measure('ammo_last_inventory_slot', choose, 250);
    } finally {
        for (let i = 0; i < inventory.length; i++) player.inventory[i] = inventory[i];
        player.selectedItemState.selected = selected; player.bodyFrame = body; player.legFrame = leg;
        player.ammoCyclingMode = cycling; player.itemAnimation = animation;
    }
}

function startPhase() {
    phase = phases[++phaseIndex];
    if (!phase) { done = true; log({ kind: 'FIM', classes: classCount, phases: phases.length }); return; }
    if (phase.startsWith('draw_') && !drawingRegistered) { drawingRegistered = true; ModPlayer.register(DrawProbe); }
    warm = 120; collecting = false; updateTimes = []; drawTimes = [];
    log({ kind: 'start', classes: classCount, phase });
}
function finishPhase() {
    collecting = false;
    const after = snapshot(), hooks = [];
    for (let i = 0; i < after.length; i++) {
        const row = after[i], old = before[i];
        if (!old || !/Player\.|PlayerDraw|Rectangle.Intersects/.test(row.name)) continue;
        const calls = row.calls - old.calls;
        if (calls) hooks.push({ index: i, name: row.name, calls, js: row.js - old.js, usPerUpdate: (row.jsMs - old.jsMs) * 1000 / updateTimes.length });
    }
    log({ kind: 'phase', classes: classCount, phase, callbacks, update: summary(updateTimes), draw: summary(drawTimes) });
    for (const hook of hooks) log({ kind: 'hook', classes: classCount, phase, ...hook });
    startPhase();
}

Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    const start = performance.now(); original(main, time); const elapsed = performance.now() - start;
    if (done || Main.gameMenu) return;
    if (phaseIndex < 0) { log({ kind: 'context', classes: classCount, netMode: Main.netMode, local: Main.myPlayer }); startPhase(); return; }
    if (warm > 0) {
        if (--warm === 0) {
            if (phaseIndex === 0) {
                const player = Main.player[Main.myPlayer];
                itemMeasurements(player);
                measure('cap_attack_speeds', () => player['void CapAttackSpeeds()'](), 4000);
                measure('item_check', () => player['void ItemCheck()'](), 4000);
            }
            before = snapshot(); callbacks = 0; collecting = true;
        }
        return;
    }
    if (!collecting) return;
    updateTimes.push(elapsed);
    if (updateTimes.length === 300) finishPhase();
});
Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    const start = performance.now(); original(main, time); const elapsed = performance.now() - start;
    if (collecting && !done && !Main.gameMenu) drawTimes.push(elapsed);
});
export default class HookPerformance extends Mod {}
