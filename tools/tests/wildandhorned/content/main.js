// Temporary in-game checks. Run in single player, then remove this test package.
const Main = Terraria.Main;
const spawn = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
let frames = 0, failures = 0, initialized = false, done = false;
let mod, pets = [], savedCulture, savedBloodMoon, savedAutoSave;
const draws = new Map();
function check(name, fn) {
    try {
        const result = fn();
        if (result !== true && result !== undefined) throw new Error(String(result));
        bl.log('wildandhorned-qa ' + name + ': PASS');
    } catch (error) {
        failures++;
        bl.log('wildandhorned-qa ' + name + ': FAIL ' + error + ' ' + error.stack);
    }
}
function finish() {
    for (const p of pets) if (p && p.ModProjectile && p.ModProjectile.Mod.id === 'wildandhorned') p.active = false;
    Main.bloodMoon = savedBloodMoon;
    Main.autoSave = savedAutoSave;
    if (savedCulture) Terraria.Localization.LanguageManager.Instance['void SetLanguage(string cultureName)'](savedCulture);
    done = true;
    bl.log('wildandhorned-qa DONE: ' + failures + ' failure(s)');
}
function setup() {
    savedCulture = ModLocalization.ActiveCultureName;
    savedBloodMoon = Main.bloodMoon;
    savedAutoSave = Main.autoSave;
    Main.autoSave = false;
    Main.bloodMoon = false;
    mod = ModLoader.GetMod('wildandhorned');
    const items = mod.GetContent(ModItem), buffs = mod.GetContent(ModBuff), projectiles = mod.GetContent(ModProjectile);
    check('16 complete families', () => items.length === 16 && buffs.length === 16 && projectiles.length === 16);
    check('native item defaults', () => {
        for (const source of items) {
            const item = Terraria.Item.new();
            item['void .ctor()']();
            item['void SetDefaults(int Type, ItemVariant variant)'](source.Type, null);
            const d = item.ModItem.Definition;
            if (item.shoot !== ModContent.ProjectileType('wildandhorned/' + d.name) ||
                item.buffType !== ModContent.BuffType('wildandhorned/' + d.buff) || item.value !== 20000)
                return d.name + ' defaults';
        }
    });
    check('native buff helper summons ordinary and light pets', () => {
        const player = Main.player[Main.myPlayer];
        let slot = -1;
        for (let i = 0; i < player.buffType.length; i++) if (!player.buffType[i]) { slot = i; break; }
        if (slot < 0) return 'No free buff slot';
        const oldTime = player.buffTime[slot];
        try {
            for (const name of ['Kasen', 'Aunn']) {
                const type = ModContent.ProjectileType('wildandhorned/' + name);
                const buff = buffs.find(b => b.constructor.name === name + 'Buff');
                player.buffType[slot] = buff.Type;
                player.buffTime[slot] = 2;
                buff.UpdatePlayer(player, slot);
                const found = [];
                for (let i = 0; i < 1000; i++) {
                    const p = Main.projectile[i];
                    if (p.active && p.owner === Main.myPlayer && p.type === type) found.push(p);
                }
                const ok = found.length === 1 && player.buffTime[slot] === 18000;
                for (const p of found) p.active = false;
                if (!ok) return name + ': ' + found.length + ' pets, buff time ' + player.buffTime[slot];
            }
        } finally {
            player.buffType[slot] = 0;
            player.buffTime[slot] = oldTime;
        }
    });
    for (const source of projectiles) {
        const prototype = source.constructor.prototype;
        for (const name of ['AI', 'PostDraw']) {
            const callback = prototype[name];
            prototype[name] = function(...args) {
                try { return callback.apply(this, args); }
                catch (error) {
                    check(this.Definition.name + '.' + name, () => { throw error; });
                    throw error;
                }
            };
        }
        const original = prototype.PreDraw;
        prototype.PreDraw = function(p, light) {
            try {
                const result = original.call(this, p, light);
                draws.set(this.Definition.name, (draws.get(this.Definition.name) || 0) + 1);
                return result;
            } catch (error) {
                check(this.Definition.name + '.PreDraw', () => { throw error; });
                throw error;
            }
        };
        const player = Main.player[Main.myPlayer];
        const index = spawn(Terraria.DataStructures.EntitySource_DebugCommand.new(),
            Vector2.new(player.Center.X, player.Center.Y - 40), Vector2.Zero,
            source.Type, 0, 0, Main.myPlayer, 0, 0, 0, null);
        const p = Main.projectile[index];
        p.timeLeft = 10000;
        pets.push(p);
    }
    check('16 native projectile instances', () => pets.length === 16 && pets.every(p => p.active && p.ModProjectile));
    check('Brazilian Portuguese', () => {
        Terraria.Localization.LanguageManager.Instance['void SetLanguage(string cultureName)']('pt-BR');
        return ModLocalization.Translate('Mods.wildandhorned.ItemTooltip.OverdueNotice').includes('Último');
    });
    for (const p of pets) p.ModProjectile.Speak(1, 0);
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || Main.gameMenu || Main.netMode !== 0 || i !== Main.myPlayer) return;
    if (!initialized) {
        if (++frames < 60) return;
        initialized = true;
        frames = 0;
        check('setup', setup);
        if (failures) { finish(); return; }
    }
    try {
        frames++;
        const kasen = pets.find(p => p.ModProjectile.Definition.name === 'Kasen');
        if (frames === 100) Main.bloodMoon = true;
        if (frames === 150) check('native Oni transition', () => mod.Call('GetKasenForm', kasen) === 'Oni');
        if (frames === 200) Main.bloodMoon = false;
        if (frames === 250) check('native Hermit transition', () => mod.Call('GetKasenForm', kasen) === 'Hermit');
        if (frames >= 300 && (frames - 300) % 300 === 0) {
            const action = Math.floor((frames - 300) / 300);
            for (const p of pets) {
                const pet = p.ModProjectile;
                if (pet.Definition.name === 'Kasen') pet.PetState = 3 + action % 2;
                else if (action < pet.Definition.actions.length) pet.PetState = 3 + action;
            }
        }
        if (frames === 1500) {
            check('all 16 sprites rendered through native SpriteBatch', () => {
                const missing = pets.filter(p => !(draws.get(p.ModProjectile.Definition.name) > 10));
                return !missing.length || missing.map(p => p.ModProjectile.Definition.name).join(', ');
            });
            check('all pets survive and remain harmless', () => pets.every(p => p.active && p.damage === 0 && !p.tileCollide));
            check('English fallback', () => {
                Terraria.Localization.LanguageManager.Instance['void SetLanguage(string cultureName)']('en-US');
                return ModLocalization.Translate('Mods.wildandhorned.ItemTooltip.OverdueNotice').includes('Final');
            });
            finish();
        }
    } catch (error) {
        check('runtime frame ' + frames, () => { throw error; });
        finish();
    }
});
bl.log('wildandhorned-qa loaded; waiting for a single-player world');
export default class WildAndHornedRuntimeChecks extends Mod {}
