// Host-side state-machine tests; native bridge calls are recorded, not emulated.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const modRoot = path.resolve(process.env.BL_WILDANDHORNED_ROOT || path.join(root, 'samples/WildAndHorned'));
const content = path.join(modRoot, 'content');
const english = JSON.parse(fs.readFileSync(path.join(content, 'Localization/en-US.json'), 'utf8'));
const portuguese = JSON.parse(fs.readFileSync(path.join(content, 'Localization/pt-BR.json'), 'utf8'));
const recorded = { draws: [], lights: [], dust: [], sounds: [], spawn: [], recipes: [] };
let randomValue = 0.9, forceChance = false, language = english;
const vec = (X = 0, Y = 0) => ({ X, Y });
const Vector2 = {
    new: vec, Add: (a, b) => vec(a.X + b.X, a.Y + b.Y),
    Subtract: (a, b) => vec(a.X - b.X, a.Y - b.Y),
    Multiply: (v, n) => vec(v.X * n, v.Y * n),
    DistanceSquared: (a, b) => (a.X - b.X) ** 2 + (a.Y - b.Y) ** 2,
    Distance: (a, b) => Math.hypot(a.X - b.X, a.Y - b.Y),
};
const Main = {
    player: [], projectile: Array.from({ length: 1001 }, () => ({ active: false })),
    npc: Array.from({ length: 201 }, () => ({ active: false })),
    projFrames: [], projPet: [], vanityPet: [], lightPet: [], buffNoTimeDisplay: [],
    tileSpelunker: [], myPlayer: 0, GameUpdateCount: 1, bloodMoon: false,
    dayTime: true, dedServ: false, maxTilesX: 1000, maxTilesY: 1000,
    screenPosition: vec(), essScale: 1,
    spriteBatch: { 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)': (...args) => recorded.draws.push(args) },
    tile: { 'Tile get_Item(int x, int y)': () => ({ type: 0, 'bool active()': () => false }) },
};
const types = new Map(), configs = new Map();
function type(name, kind) { return types.get(kind + ':' + name.split('/').at(-1)) || 0; }
class ModProjectile { get Projectile() { return this.entity; } }
class ModItem {
    get Item() { return this.entity; }
    CreateRecipe() {
        const recipe = { result: this.Type, ingredients: [], tile: null,
            AddIngredient(id, amount = 1) { this.ingredients.push({ id, amount, group: false }); return this; },
            AddRecipeGroup(id, amount = 1) { this.ingredients.push({ id, amount, group: true }); return this; },
            AddTile(id) { this.tile = id; return this; },
            Register() { recorded.recipes.push(this); },
        };
        return recipe;
    }
}
class ModBuff {}
class Mod {}
class ModSystem {}
class ModConfig {
    static Toggle(value) { return { value }; }
    static Range(value, options) { return { value, ...options }; }
}
const ID = new Proxy({}, { get: (_, key) => key });
const font = { 'Vector2 MeasureString(string text)': text => vec(text.length * 8, 24) };
const context = vm.createContext({
    ModProjectile, ModItem, ModBuff, Mod, ModSystem, ModConfig,
    Vector2, Rectangle: { new: (X, Y, Width, Height) => ({ X, Y, Width, Height }) },
    Color: { new: (R, G, B, A = 255) => ({ R, G, B, A }) },
    Rand: { NextBool: () => forceChance, NextFloat: (a = 0, b = 1) => b === 1 ? randomValue * (a || 1) : a + randomValue * (b - a), Next: n => Math.floor(randomValue * n) },
    Ref: class { constructor(value) { this.value = value; } },
    NPCShop: { get: () => null },
    ModLocalization: { Translate: key => key.replace('Mods.wildandhorned.', '').split('.').reduce((v, k) => v?.[k], language) || key },
    ModContent: {
        ProjectileType: name => type(name, 'projectile'), BuffType: name => type(name, 'buff'), ItemType: name => type(name, 'item'), NPCType: () => 0,
        GetInstance: cls => configs.get(cls.name),
        Texture: name => {
            const file = fs.readFileSync(path.join(content, name.replace('wildandhorned/', '') + '.png'));
            return { Width: file.readUInt32BE(16), Height: file.readUInt32BE(20) };
        },
    },
    Terraria: {
        Main,
        ID: { ItemID: ID, TileID: ID, ItemRarityID: ID, DustID: ID, SoundID: { Item8: 'Item8', Dig: 0, MenuTick: 12 }, ProjectileID: { Sets: { LightPet: [] } } },
        Player: { 'void BuffHandle_SpawnPetIfNeededAndSetTime(int buffIndex, ref bool petBool, int petProjID, int buffTimeToGive)': (...args) => recorded.spawn.push(args) },
        Dust: { 'Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)': (...args) => { recorded.dust.push(args); return {}; } },
        Lighting: { 'void AddLight(Vector2 position, float r, float g, float b)': (...args) => recorded.lights.push(args) },
        Audio: { SoundEngine: {
            'SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)': (...args) => recorded.sounds.push(args),
            'SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)': (...args) => recorded.sounds.push(args),
        } },
        GameContent: { FontAssets: { MouseText: { Value: font } } },
        Utils: { 'void DrawBorderStringFourWay(SpriteBatch sb, SpriteFont font, string text, float x, float y, Color textColor, Color borderColor, Vector2 origin, float scale)': (...args) => recorded.draws.push(args) },
    },
});
const modules = new Map();
async function load(file) {
    file = path.resolve(file);
    if (modules.has(file)) return modules.get(file);
    const mod = new vm.SourceTextModule(fs.readFileSync(file, 'utf8'), { context, identifier: file });
    modules.set(file, mod);
    await mod.link((specifier, parent) => load(path.resolve(path.dirname(parent.identifier), specifier)));
    return mod;
}
const entry = await load(path.join(content, 'main.js'));
await entry.evaluate();
const petModules = [];
for (const file of fs.readdirSync(path.join(content, 'Content/Pets')).filter(f => f.endsWith('.js'))) {
    const mod = await load(path.join(content, 'Content/Pets', file));
    await mod.evaluate(); petModules.push(mod);
}
const defs = modules.get(path.join(content, 'Common/Pets/Definitions.js')).namespace.PETS;
const logic = modules.get(path.join(content, 'Common/Pets/Logic.js')).namespace;
const runtime = modules.get(path.join(content, 'Common/Pets/Runtime.js')).namespace;
const effects = modules.get(path.join(content, 'Common/Pets/Effects.js')).namespace;
const configClass = modules.get(path.join(content, 'Common/Configs/KasenConfig.js')).namespace.KasenConfig;
configs.set('KasenConfig', Object.fromEntries(Object.entries(configClass.Options).map(([k, v]) => [k, v.value])));
let nextType = 800;
const classes = {};
for (const module of petModules) for (const [name, cls] of Object.entries(module.namespace)) {
    if (cls.prototype instanceof ModProjectile) { types.set('projectile:' + name, nextType++); classes[name] = cls; }
    else if (cls.prototype instanceof ModBuff) { types.set('buff:' + name, nextType++); classes[name] = cls; }
    else if (cls.prototype instanceof ModItem) { types.set('item:' + name, nextType++); classes[name] = cls; }
}
Main.player[0] = { active: true, dead: false, direction: 1, gfxOffY: 0, whoAmI: 0,
    Center: vec(5000, 5000), MountedCenter: vec(5000, 5000), FindBuffIndex: () => 0,
    AddBuff: (...args) => recorded.spawn.push(args) };
Main.player[1] = { ...Main.player[0], whoAmI: 1 };
let slot = 0;
function pet(name, owner = 0) {
    const instance = new classes[name]();
    instance.Type = type(name, 'projectile');
    const projectile = { type: instance.Type, owner, whoAmI: slot++, active: true,
        Center: vec(5000, 5000), position: vec(4975, 4975), velocity: vec(), ai: [0, 0, 0],
        frame: 0, spriteDirection: -1, rotation: 0, scale: 1, alpha: 0,
        'Color GetAlpha(Color newColor)': color => color };
    instance.entity = projectile; projectile.ModProjectile = instance;
    Main.projectile[projectile.whoAmI] = projectile;
    instance.SetDefaults(projectile); instance.SetStaticDefaults();
    return instance;
}
let checks = 0;
function check(name, fn) { fn(); checks++; console.log('PASS ' + name); }
function flatten(value, prefix = '') {
    return Object.entries(value).flatMap(([k, v]) => typeof v === 'object' ? flatten(v, prefix + k + '.') : [[prefix + k, v]]);
}

check('16 complete pet families and seven light pets', () => {
    assert.equal(Object.keys(defs).length, 16); assert.equal(Object.keys(classes).length, 48);
    assert.equal(Object.values(defs).filter(d => d.light).length, 7);
});
check('all original textures and cells fit every animation', () => {
    for (const d of Object.values(defs)) {
        for (const name of [d.name, d.buff, d.item]) assert.ok(fs.existsSync(path.join(content, 'Content/Pets', name + '.png')));
        const buffer = fs.readFileSync(path.join(content, 'Content/Pets', d.name + '.png'));
        assert.equal(buffer.readUInt32BE(16) % d.columns, 0);
        assert.equal(buffer.readUInt32BE(20) % d.frames, 0);
        for (const a of d.actions) for (const [frame, ticks] of a.frames) { assert.ok(frame >= 0 && frame < d.frames); assert.ok(ticks > 0); }
    }
});
check('412 matching localized text keys and all dialogue references', () => {
    const en = flatten(english), pt = flatten(portuguese);
    assert.equal(en.length, 412); assert.deepEqual(pt.map(v => v[0]).sort(), en.map(v => v[0]).sort());
    assert.ok(pt.every(([, v]) => typeof v === 'string' && v.length));
    for (const d of Object.values(defs)) {
        const lines = [...d.regular, ...d.companions.map(c => c.line), ...d.actions.flatMap(a => [...a.lines, ...(a.alertLines || [])])];
        for (const line of lines) assert.ok(english['Chat_' + d.name]['Chat' + line]);
        for (const c of d.companions) assert.ok(english.Replies[d.name + c.line]);
    }
});
check('17 recipes preserve materials, wood/iron groups and both watches', () => {
    for (const d of Object.values(defs)) { const item = new classes[d.item](); item.Type = type(d.item, 'item'); item.AddRecipes(); }
    assert.equal(recorded.recipes.length, 17);
    assert.equal(recorded.recipes.filter(r => r.ingredients.some(i => i.group && i.id === 'Wood')).length, 3);
    assert.equal(recorded.recipes.filter(r => r.ingredients.some(i => i.group && i.id === 'IronBar')).length, 3);
    assert.ok(recorded.recipes.some(r => r.ingredients.some(i => i.id === 'SilverWatch')));
    assert.ok(recorded.recipes.some(r => r.ingredients.some(i => i.id === 'TungstenWatch')));
});
check('every summon applies a buff and suppresses duplicate shooting', () => {
    for (const d of Object.values(defs)) {
        const item = new classes[d.item](), calls = [];
        item.entity = { 'void DefaultToVanitypet(int projId, int buffID)': (shoot, buffType) => Object.assign(item.entity, { shoot, buffType }) };
        item.SetDefaults(); assert.equal(item.Item.shoot, type(d.name, 'projectile')); assert.equal(item.Item.buffType, type(d.buff, 'buff'));
        assert.equal(item.Shoot(item.Item, { AddBuff: (...args) => calls.push(args) }), false);
        assert.equal(calls[0][0], type(d.buff, 'buff')); assert.equal(item.Item.value, 20000);
        const buff = new classes[d.buff](); buff.Type = type(d.buff, 'buff'); buff.SetStaticDefaults(); buff.UpdatePlayer(Main.player[0], 1);
        assert.equal(Main.lightPet[buff.Type], d.light); assert.equal(recorded.spawn.at(-1)[3], type(d.name, 'projectile'));
    }
});
check('buff lifetime, inactive owners and zero contact/tile damage', () => {
    const p = pet('Yuugi'); assert.equal(p.PreAI(p.Projectile), true); assert.equal(p.Projectile.timeLeft, 2);
    assert.equal(p.CanDamage(), false); assert.equal(p.CanCutTiles(), false);
    Main.player[0].dead = true; p.Projectile.timeLeft = 1; p.PreAI(p.Projectile); assert.equal(p.Projectile.timeLeft, 1); Main.player[0].dead = false;
    Main.player[0].active = false; assert.equal(p.PreAI(p.Projectile), false); assert.equal(p.Projectile.active, false); Main.player[0].active = true;
});
check('animation boundaries, weighted selection and layer exclusions', () => {
    assert.equal(logic.animationFrame([[1, 2], [2, 3]], 1).frame, 1);
    assert.equal(logic.animationFrame([[1, 2], [2, 3]], 2).frame, 2);
    assert.equal(logic.animationFrame([[1, 2], [2, 3]], 5).done, true);
    assert.equal(logic.weightedIndex([1, 6], 0.99), 1);
    assert.equal(logic.hidesLayers('Mamizou', 11), true); assert.equal(logic.hidesLayers('Suwako', 3), true); assert.equal(logic.hidesLayers('Shion', 6), true);
});
check('all 39 roster actions complete and render the correct sprite cells', () => {
    let actions = 0;
    for (const d of Object.values(defs).filter(d => d.name !== 'Kasen')) {
        const p = pet(d.name);
        for (let index = 0; index < d.actions.length; index++) {
            const action = d.actions[index]; p.Projectile.ai[1] = index + 3; p.lastState = index + 3; p.timer = 0;
            assert.equal(p.CurrentAction, action.name);
            const duration = action.frames.reduce((sum, f) => sum + f[1], 0);
            for (let t = 0; t <= duration; t++) { p.AI(p.Projectile); p.PostAI(p.Projectile); p.PreDraw(p.Projectile, { R: 255, G: 255, B: 255, A: 255 }); }
            assert.equal(p.PetState, 0); assert.ok(p.cooldown > 0); actions++;
        }
    }
    assert.equal(actions, 39);
});
check('remote clients never choose actions, forms or dialogue', () => {
    forceChance = true;
    const p = pet('Kasen', 1); p.mainTimer = 360; p.Idle(); p.TargetForm = 'Oni'; p.Speak(1); p.PetState = 4;
    assert.deepEqual(p.Projectile.ai, [0, 0, 0]);
    p.Projectile.ai[0] = 1; p.UpdateForm(); assert.equal(p.DisplayedForm, 'Oni');
    forceChance = false;
});
check('Kasen spawns in oni form during a Blood Moon', () => {
    Main.bloodMoon = true; const p = pet('Kasen'); p.UpdateForm();
    assert.equal(p.TargetForm, 'Oni'); assert.equal(p.DisplayedForm, 'Oni'); assert.equal(p.IsTransforming, false); Main.bloodMoon = false;
});
check('Kasen changes form behind the smoke and returns after the moon', () => {
    const p = pet('Kasen'); p.UpdateForm(); Main.bloodMoon = true; p.UpdateForm();
    assert.equal(p.CurrentAction, 'Transform'); assert.equal(p.DisplayedForm, 'Hermit');
    for (let i = 0; i < 16; i++) p.UpdateForm(); assert.equal(p.DisplayedForm, 'Oni');
    for (let i = 0; i < 20; i++) p.UpdateForm(); assert.equal(p.IsTransforming, false);
    Main.bloodMoon = false; for (let i = 0; i < 37; i++) p.UpdateForm(); assert.equal(p.DisplayedForm, 'Hermit');
    assert.equal(logic.kasenBodyFrame(3, true, 0), 11); assert.equal(logic.kasenBodyFrame(4, true, 0), 12);
});
check('Kasen settings disable both automatic transformation paths', () => {
    const config = configs.get('KasenConfig'); config.BloodMoonOniForm = false; config.SpontaneousShifts = false;
    Main.bloodMoon = true; forceChance = true; const p = pet('Kasen'); p.UpdateForm(); p.UpdateForm();
    assert.equal(p.DisplayedForm, 'Hermit'); assert.equal(p.spontaneousTicksLeft, 0);
    Main.bloodMoon = false; forceChance = false; config.BloodMoonOniForm = config.SpontaneousShifts = true;
});
check('repeated dialogue synchronizes and language changes update displayed text', () => {
    const p = pet('Kasen'); p.Speak(1); const first = p.Projectile.ai[2]; p.Speak(1); assert.notEqual(p.Projectile.ai[2], first);
    p.chatLag = 0; language = portuguese; p.PostDraw(); assert.ok(recorded.draws.at(-1)[2].length);
    assert.ok(recorded.draws.some(args => typeof args[2] === 'string' && args[2].includes('Uma eremita'))); language = english;
});
check('nearby companions answer and other owners do not participate', () => {
    Main.projectile.fill({ active: false }); slot = 0;
    const kasen = pet('Kasen'), komachi = pet('Komachi'); kasen.Speak(23, 0); assert.equal(kasen.replyIndex, komachi.Projectile.whoAmI);
    kasen.chatLife = 0; kasen.UpdateChat(); assert.equal(komachi.chatKey, 'Replies.Kasen23');
    assert.equal(runtime.companion(kasen, 'Reimu'), null);
    komachi.Projectile.owner = 1; assert.equal(runtime.companion(kasen, 'Komachi'), null);
    komachi.Projectile.owner = 0; kasen.Speak(23, 0); kasen.chatLife = 0;
    const replacement = pet('Yuugi'); Main.projectile[komachi.Projectile.whoAmI] = replacement.Projectile;
    kasen.UpdateChat(); assert.equal(replacement.chatKey, '');
    const code = replacement.Projectile.ai[2]; replacement.Speak('UnknownReply', 0, true);
    assert.equal(replacement.Projectile.ai[2], code);
});
check('pets avoid occupied positions and teleport back across large distances', () => {
    Main.projectile.fill({ active: false }); slot = 0; Main.GameUpdateCount = 20;
    const a = pet('Yuugi'), b = pet('Komachi'); b.Projectile.Center = vec(4950, 4970); runtime.movePet(a); assert.notEqual(a.followSpot, 0);
    a.Projectile.Center = vec(10000, 10000); runtime.movePet(a); assert.ok(Vector2.Distance(a.Projectile.Center, Main.player[0].Center) < 200);
});
check('seven light colors preserve enemy, reading, treasure and night responses', () => {
    assert.ok(logic.lightColor('Aunn', 1, { alert: 30 })[1] < logic.lightColor('Aunn', 1)[1]);
    assert.ok(logic.lightColor('Kosuzu', 1, { aura: 40 })[2] > logic.lightColor('Kosuzu', 1)[2]);
    assert.ok(logic.lightColor('Nazrin', 1, { glow: 30 })[0] > logic.lightColor('Nazrin', 1)[0]);
    assert.ok(logic.lightColor('Okina', 1, { day: false })[0] > logic.lightColor('Okina', 1)[0]);
    const p = pet('Nazrin'); effects.addPetLight(p); assert.equal(recorded.lights.at(-1).length, 4);
});
check('companion query API retains stable form and action names', () => {
    const api = new entry.namespace.default(), p = pet('Kasen'); p.DisplayedForm = 'Oni';
    assert.equal(api.Call('GetKasenForm', p.Projectile), 'Oni'); assert.equal(api.Call('GetPetHouseStoriesVariant', p.Projectile), 'Oni');
    p.smokeTimer = 10; assert.equal(api.Call('IsKasenTransforming', p.Projectile), true); assert.equal(api.Call('GetPetAction', p.Projectile), 'Transform');
    assert.equal(api.Call('GetPetAction', {}), null);
    assert.equal(api.Call('IsKasenTransforming', {}), false);
});
check('numeric and legacy sound IDs use their correct native overloads', () => {
    const p = pet('Kanako'); effects.sound(p, 'Dig', -0.3, 0.5);
    assert.equal(recorded.sounds.at(-1).length, 6);
    assert.deepEqual(recorded.sounds.at(-1).slice(3), [1, 0.5, -0.3]);
    effects.sound(p, 'Item8', 0.2, 0.45); assert.equal(recorded.sounds.at(-1).length, 4);
});
console.log(`Wild and Horned: ${checks} checks passed.`);
