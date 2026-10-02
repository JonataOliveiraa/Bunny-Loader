// TileCollideStyle e OnTileCollide de lacaio. Uma fileira de plataformas 8
// blocos acima do jogador; projéteis de mod com gravidade própria caem nela:
//   StandSmall (40x40, caixa 8x8, fallThrough false): para na plataforma com
//     o centro 4 px acima dela (a caixa, não o desenho, encosta);
//   AnchorTop (caixa 8x8, âncora 0,5/1): a caixa fica acima do centro, o
//     centro encosta na plataforma;
//   FallControl (40x40, sem TileCollideStyle): projétil comum atravessa;
//   PlatformPet (pet, fallThrough false): o pet para na plataforma;
//   Ghost (TileCollideStyle false): atravessa a plataforma e o chão, sem
//     OnTileCollide, e o tileCollide dele continua true;
//   GroundPet (pet, sem TileCollideStyle): cai no chão e recebe o
//     OnTileCollide todo quadro, sem morrer.
// Loga "collidestyle <caso>: ok | FALHOU".
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('collidestyle ' + label + ': ok');
        else { fails++; bl.log('collidestyle ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('collidestyle ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const hits = {};
const lastHit = {};
const tileCollideSeen = {};
function countHit(proj, name, oldVelocity) {
    hits[name] = (hits[name] || 0) + 1;
    lastHit[name] = { old: oldVelocity.Y, now: proj.velocity.Y };
}

class Faller extends ModProjectile {
    Texture = 'Box';
    Size = 40;

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = this.Size;
        this.Projectile.aiStyle = -1;
        this.Projectile.tileCollide = true;
        this.Projectile.timeLeft = 600;
    }

    AI(proj) {
        tileCollideSeen[this.constructor.name] = proj.tileCollide;
        proj.velocity = Vector2.new(0, Math.min(proj.velocity.Y + 0.3, 10));
    }
}

export class StandSmall extends Faller {
    TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac) {
        width.value = height.value = 8;
        fallThrough.value = false;
        return true;
    }
    OnTileCollide(proj, oldVelocity) {
        countHit(proj, 'StandSmall', oldVelocity);
        return false;
    }
}

export class AnchorTop extends Faller {
    TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac) {
        width.value = height.value = 8;
        fallThrough.value = false;
        hitboxCenterFrac.value = Vector2.new(0.5, 1);
        return true;
    }
    OnTileCollide(proj, oldVelocity) { return false; }
}

const trail = [];
export class FallControl extends Faller {
    AI(proj) {
        super.AI(proj);
        if (trail.length < 40) trail.push(proj.Center.Y.toFixed(1) + '/' + proj.velocity.Y.toFixed(2));
    }
    OnTileCollide(proj, oldVelocity) {
        countHit(proj, 'FallControl', oldVelocity);
        return false;
    }
}

export class PlatformPet extends Faller {
    Size = 20;
    SetStaticDefaults() { Main.projPet[this.Type] = true; }
    TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac) {
        fallThrough.value = false;
        return true;
    }
    OnTileCollide(proj, oldVelocity) {
        countHit(proj, 'PlatformPet', oldVelocity);
        return true;
    }
}

export class Ghost extends Faller {
    Size = 20;
    TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac) { return false; }
    OnTileCollide(proj, oldVelocity) {
        countHit(proj, 'Ghost', oldVelocity);
        return false;
    }
}

export class GroundPet extends Faller {
    Size = 20;
    SetStaticDefaults() { Main.projPet[this.Type] = true; }
    OnTileCollide(proj, oldVelocity) {
        countHit(proj, 'GroundPet', oldVelocity);
        return true;
    }
}

const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const placeTile = WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'];
const killTile = WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];

const me = () => Main.player[Main.myPlayer];
const typeOf = (name) => ModProjectile.getTypeByName(name);
const fmt = (v) => `(${v.X.toFixed(1)}, ${v.Y.toFixed(1)})`;

let platY = 0, platformTop = 0, placed = [], spawned = {}, playerBottom = 0;

function spawn(name, x, y) {
    const i = newProj(Terraria.DataStructures.EntitySource_DebugCommand.new(), x, y, 0, 0, typeOf(name), 0, 0, Main.myPlayer, 0, 0, 0, null);
    spawned[name] = Main.projectile[i];
}

function setUp() {
    const p = me();
    const px = Math.floor(p.Center.X / 16);
    playerBottom = p.position.Y + p.height;

    // Ar acima e logo abaixo da plataforma: um bloco solto ali segura quem
    // devia atravessar. Sobe até achar a faixa livre.
    const head = Math.floor(p.position.Y / 16);
    const free = (row) => !solid(Vector2.new((px - 13) * 16, (row - 6) * 16), 27 * 16, 11 * 16);
    platY = head - 8;
    while (platY > head - 30 && !free(platY)) platY--;
    if (!free(platY)) return 'sem faixa de ar livre acima do jogador em ' + px + ',' + head;
    platformTop = platY * 16;

    for (let i = px - 12; i <= px + 12; i++) {
        if (placeTile(i, platY, 19, true, true, -1, 0)) placed.push(i);
    }
    if (placed.length < 25) return 'plataformas postas: ' + placed.length + ' de 25';

    const y = platformTop - 4 * 16;
    spawn('StandSmall', (px - 8) * 16 + 8, y);
    spawn('AnchorTop', (px - 4) * 16 + 8, y);
    spawn('FallControl', px * 16 + 8, y);
    spawn('PlatformPet', (px + 4) * 16 + 8, y);
    spawn('Ghost', (px + 8) * 16 + 8, y);
    spawn('GroundPet', p.Center.X - 20 * 16, p.position.Y - 2 * 16);
    return true;
}

function report() {
    for (const [name, pr] of Object.entries(spawned)) {
        bl.log(`collidestyle diag: ${name} centro ${fmt(pr.Center)} vel ${fmt(pr.velocity)} ativo ${pr.active} ` +
               `choques ${hits[name] || 0} correctSlope ${pr.correctSlopeCollision}`);
    }
    bl.log(`collidestyle diag: plataforma no topo y ${platformTop}, pé do jogador ${playerBottom.toFixed(1)}`);
    bl.log('collidestyle diag: FallControl y/vy ' + trail.join(' ') + ' | ultimo choque ' + JSON.stringify(lastHit.FallControl));
    const col = Math.floor(spawned.FallControl.Center.X / 16);
    const rows = [];
    for (let j = platY - 1; j <= platY + 4; j++) rows.push(j + ':' + [-1, 0, 1].map((d) => bl.tiles.typeAt(col + d, j)).join('/'));
    bl.log('collidestyle diag: tipos em volta da plataforma (coluna ' + col + ') ' + rows.join(' '));

    const restsAt = (name, centerY) => {
        const pr = spawned[name];
        return (pr.active && Math.abs(pr.Center.Y - centerY) < 2 && pr.velocity.Y === 0) ||
            `centro y ${pr.Center.Y.toFixed(1)} (esperado ${centerY}), vy ${pr.velocity.Y.toFixed(2)}, ativo ${pr.active}`;
    };

    check('StandSmall: a caixa 8x8 encosta na plataforma (centro 4 px acima)', () => restsAt('StandSmall', platformTop - 4));
    check('StandSmall: OnTileCollide no choque com a plataforma', () => (hits.StandSmall || 0) > 10 || 'choques ' + hits.StandSmall);
    check('AnchorTop: âncora 0,5/1 põe o centro na plataforma', () => restsAt('AnchorTop', platformTop));
    check('FallControl: sem TileCollideStyle, atravessa a plataforma', () =>
        spawned.FallControl.Center.Y > platformTop + 16 || 'centro y ' + spawned.FallControl.Center.Y.toFixed(1));
    check('PlatformPet: pet com fallThrough false para na plataforma', () => restsAt('PlatformPet', platformTop - 10));
    check('Ghost: TileCollideStyle false atravessa plataforma e chão', () =>
        spawned.Ghost.Center.Y > playerBottom + 32 || 'centro y ' + spawned.Ghost.Center.Y.toFixed(1));
    check('Ghost: sem OnTileCollide', () => !hits.Ghost || 'choques ' + hits.Ghost);
    check('Ghost: o tileCollide volta a true depois do movimento', () => tileCollideSeen.Ghost === true || 'tileCollide ' + tileCollideSeen.Ghost);
    check('GroundPet: pet no chão recebe OnTileCollide todo quadro', () => (hits.GroundPet || 0) > 60 || 'choques ' + hits.GroundPet);
    check('GroundPet: oldVelocity é a de antes do choque', () => {
        const h = lastHit.GroundPet;
        return (h && h.old > 0 && h.now === 0) || JSON.stringify(h);
    });
    check('GroundPet: continua vivo (pet não morre no bloco)', () => spawned.GroundPet.active || 'morreu');
}

function cleanUp() {
    for (const pr of Object.values(spawned)) pr.active = false;
    for (const i of placed) killTile(i, platY, false, false, true);
}

let frames = 0, started = -1, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;

    if (started < 0) {
        started = frames;
        const ready = setUp();
        check('preparo', () => ready);
        if (ready !== true) {
            cleanUp();
            done = true;
            bl.log('collidestyle FIM: ' + fails + ' falha(s)');
        }
        return;
    }

    // Todo quadro: o jogador fica parado e longe das quedas.
    self.fallStart = Math.floor(self.position.Y / 16);
    if (frames - started < 180) return;

    report();
    cleanUp();
    done = true;
    bl.log('collidestyle FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('collidestyle: carregado');

export default class TestCollideStyle extends Mod {}
