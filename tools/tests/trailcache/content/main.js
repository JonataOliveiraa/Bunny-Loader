// O rastro dos projeteis (bl.log com 'trailcache ...'): oldPos, oldRot e
// oldSpriteDirection de projetil de mod com ProjectileID.Sets.TrailingMode e
// TrailCacheLength no SetStaticDefaults, ao lado de projeteis do jogo com o
// mesmo modo. Depois de alguns quadros voando, o rastro tem de ter o
// tamanho pedido e posicoes diferentes de zero.
const Main = Terraria.Main;
const { ProjectileID } = Terraria.ID;
const newProjectile = Terraria.Projectile[
    'int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, ' +
    'int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, ' +
    'NewProjectileModifier modifer)'];

class TrailShot extends ModProjectile {
    SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[this.Type] = 8;
        ProjectileID.Sets.TrailingMode[this.Type] = 0;
    }
    SetDefaults() {
        this.Projectile.width = 8;
        this.Projectile.height = 8;
        this.Projectile.aiStyle = 0;
        this.Projectile.friendly = true;
        this.Projectile.timeLeft = 120;
        this.Projectile.tileCollide = false;
    }
}

// O modo 2 tambem guarda a rotacao e a direcao do sprite.
class TrailSpin extends ModProjectile {
    SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[this.Type] = 6;
        ProjectileID.Sets.TrailingMode[this.Type] = 2;
    }
    SetDefaults() {
        this.Projectile.width = 8;
        this.Projectile.height = 8;
        this.Projectile.aiStyle = 0;
        this.Projectile.friendly = true;
        this.Projectile.timeLeft = 120;
        this.Projectile.tileCollide = false;
    }
    AI(p) {
        p.rotation += 0.3;
        p.spriteDirection = p.velocity.X > 0 ? 1 : -1;
    }
}

// Como um tiro de espada: IA de bala (AIType), extraUpdates, bate em bloco e
// fura; o rastro lido por this.Projectile no AI e no PreDraw, como num mod.
const seen = { ai: null, draw: null };
const firstPos = (p) => `${p.oldPos[0].X.toFixed(0)},${p.oldPos[0].Y.toFixed(0)}`;
class TrailBeam extends ModProjectile {
    SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[this.Projectile.type] = 5;
        ProjectileID.Sets.TrailingMode[this.Projectile.type] = 0;
    }
    SetDefaults() {
        this.Projectile.width = 16;
        this.Projectile.height = 16;
        this.Projectile.aiStyle = 1;
        this.AIType = ProjectileID.Bullet;
        this.Projectile.friendly = true;
        this.Projectile.melee = true;
        this.Projectile.penetrate = 3;
        this.Projectile.timeLeft = 120;
        this.Projectile.extraUpdates = 1;
        this.Projectile.tileCollide = true;
    }
    AI() { seen.ai = firstPos(this.Projectile); }
    PreDraw(p, lightColor) { seen.draw = firstPos(this.Projectile); return true; }
}

class TrailSword extends ModItem {
    SetDefaults() {
        this.Item.melee = true;
        this.Item.shoot = ModProjectile.getTypeByName('TrailBeam');
        this.Item.shootSpeed = 8;
        this.SetWeaponValues(10, 1, 0);
        this.SetDefaultWeaponStyle(20, true);
    }
}

const SHOT = ModProjectile.register(TrailShot);
const SPIN = ModProjectile.register(TrailSpin);
const BEAM = ModProjectile.register(TrailBeam);
const SWORD = ModItem.register(TrailSword);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('trailcache ' + label + ': ok');
        else { fails++; bl.log('trailcache ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('trailcache ' + label + ': FALHOU com ' + e);
    }
}

// O primeiro tipo do jogo com aquele TrailingMode (controle).
function vanillaWithMode(mode) {
    const modes = ProjectileID.Sets.TrailingMode;
    for (let t = 1; t < bl.projectiles.vanillaCount; t++) if (modes[t] === mode) return t;
    return -1;
}

function describe(p) {
    const n = Math.min(p.oldPos.Length, 4);
    const pos = [], rot = [], dir = [];
    for (let i = 0; i < n; i++) {
        pos.push(`${p.oldPos[i].X.toFixed(0)},${p.oldPos[i].Y.toFixed(0)}`);
        rot.push(p.oldRot[i].toFixed(2));
        dir.push(p.oldSpriteDirection[i]);
    }
    return `tipo ${p.type} ativo=${p.active} Length pos/rot/dir=${p.oldPos.Length}/${p.oldRot.Length}/${p.oldSpriteDirection.Length} ` +
        `TrailCacheLength=${ProjectileID.Sets.TrailCacheLength[p.type]} TrailingMode=${ProjectileID.Sets.TrailingMode[p.type]} ` +
        `pos=[${pos.join(' ')}] rot=[${rot.join(' ')}] dir=[${dir.join(' ')}] agora=${p.position.X.toFixed(0)},${p.position.Y.toFixed(0)}`;
}

const filled = (p) => p.oldPos.Length > 1 && (p.oldPos[0].X !== 0 || p.oldPos[0].Y !== 0) &&
    (p.oldPos[1].X !== 0 || p.oldPos[1].Y !== 0);

let shots = null;
function spawn() {
    const pl = Main.LocalPlayer;
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const x = pl.position.X, y = pl.position.Y - 80;
    const make = (type, dy) => Main.projectile[newProjectile(source, x, y + dy, 3, 0, type, 0, 0, Main.myPlayer, 0, 0, 0, null)];
    const v0 = vanillaWithMode(0), v2 = vanillaWithMode(2);
    bl.log(`trailcache: controles do jogo: modo 0 = ${v0}, modo 2 = ${v2}`);
    shots = { mod0: make(SHOT, 0), mod2: make(SPIN, -20) };
    if (v0 > 0) shots.van0 = make(v0, -40);
    if (v2 > 0) shots.van2 = make(v2, -60);
    for (const [k, p] of Object.entries(shots)) bl.log(`trailcache nasceu ${k}: ${describe(p)}`);

    // O tiro da espada pelo caminho do item.
    const sword = Terraria.Item.new();
    sword['void .ctor()']();
    sword['void SetDefaults(int Type, ItemVariant variant)'](SWORD, null);
    const before = new Set();
    for (let i = 0; i < Main.projectile.length; i++) if (Main.projectile[i].active) before.add(i);
    pl['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](Main.myPlayer, sword, 10, false);
    for (let i = 0; i < Main.projectile.length; i++) {
        const p = Main.projectile[i];
        if (p.active && !before.has(i) && p.type === BEAM) shots.beam = p;
    }
    bl.log('trailcache espada: ' + (shots.beam ? describe(shots.beam) : 'o tiro nao saiu'));
}

function after() {
    for (const [k, p] of Object.entries(shots)) bl.log(`trailcache depois ${k}: ${describe(p)}`);
    check('mod modo 0: Length = TrailCacheLength (8)', () => shots.mod0.oldPos.Length === 8 || shots.mod0.oldPos.Length);
    check('mod modo 0: oldPos preenchido', () => filled(shots.mod0) || describe(shots.mod0));
    check('mod modo 2: Length = 6', () => shots.mod2.oldPos.Length === 6 || shots.mod2.oldPos.Length);
    check('mod modo 2: oldPos preenchido', () => filled(shots.mod2) || describe(shots.mod2));
    check('mod modo 2: oldRot guarda a rotacao', () => shots.mod2.oldRot[1] !== 0 || describe(shots.mod2));
    check('mod modo 2: oldSpriteDirection', () => shots.mod2.oldSpriteDirection[1] === 1 || describe(shots.mod2));
    bl.log(`trailcache espada pelo this.Projectile: AI ${seen.ai}, PreDraw ${seen.draw}`);
    check('espada (item, AIType, extraUpdates): oldPos preenchido', () =>
        (shots.beam && (filled(shots.beam) || !shots.beam.active)) || (shots.beam ? describe(shots.beam) : 'sem tiro'));
    check('espada: this.Projectile ve o rastro no AI e no PreDraw', () =>
        (seen.ai && seen.ai !== '0,0' && seen.draw && seen.draw !== '0,0') || `AI ${seen.ai}, PreDraw ${seen.draw}`);
    if (shots.van0) check('jogo modo 0: oldPos preenchido (controle)', () => !shots.van0.active || filled(shots.van0) || describe(shots.van0));
    for (const p of Object.values(shots)) if (p.active) p['void Kill()']();
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 90) check('preparo', spawn);
    if (frames === 110) {
        done = true;
        if (shots) check('depois', after);
        bl.log('trailcache FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('trailcache: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestTrailcache extends Mod {}
