// Ganchos de mod (o AI_007 do jogo) e o ShouldUpdatePosition, num piso com
// teto no alto, longe do spawn. Começa no quadro 400: o itempickup roda junto
// e usa o jogador antes.
//   - GrappleRange 100: no ar, volta a ~100 px; sem o método, a ~300 (o padrão);
//   - GrappleRetreatSpeed 30: a volta corre a 30 (o jogo: 11);
//   - NumGrappleHooks 5: cinco presos no teto (o jogo solta acima de 3); o
//     sexto solta o mais velho;
//   - GrapplePullSpeed 3 e GrappleTargetPoint +200 em X: o puxão no jogador
//     não passa de 3 e vai para a direita;
//   - ShouldUpdatePosition false: o projétil de mod e, por um GlobalProjectile,
//     uma flecha do jogo ficam no lugar (com velocidade); o de controle anda.
// Loga "projgrapple <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { ProjectileID, TileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('projgrapple ' + label + ': ok');
        else { fails++; bl.log('projgrapple ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('projgrapple ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];
const distance = (p) => {
    const c = me().MountedCenter;
    return Math.hypot(c.X - p.Center.X, c.Y - p.Center.Y);
};

// Onde cada gancho começou a voltar, e a maior velocidade na volta.
const retract = new Map();
function watchRetract(p, key) {
    if (p.ai[0] !== 1) return;
    const r = retract.get(key) || { at: distance(p), speed: 0 };
    r.speed = Math.max(r.speed, Math.hypot(p.velocity.X, p.velocity.Y));
    retract.set(key, r);
}

class TestHook extends ModProjectile {
    Texture = 'Gem';
    SetDefaults() {
        this.CloneDefaults(ProjectileID.GemHookAmethyst);
        this.Projectile.width = this.Projectile.height = 18;
    }
}

export class ShortHook extends TestHook {
    GrappleRange(proj) { return 100; }
    GrappleRetreatSpeed(proj, player, speed) { speed.value = 30; }
    PostAI(proj) { watchRetract(proj, 'short'); }
}

export class PlainHook extends TestHook {
    PostAI(proj) { watchRetract(proj, 'plain'); }
}

export class ManyHook extends TestHook {
    NumGrappleHooks(proj, player, numHooks) { numHooks.value = 5; }
}

const pulls = [];
export class SlowHook extends TestHook {
    GrapplePullSpeed(proj, player, speed) { speed.value = 3; }
    GrappleTargetPoint(proj, player, grappleX, grappleY) { grappleX.value += 200; }
}

export class Frozen extends ModProjectile {
    Texture = 'Gem';
    SetDefaults() {
        const p = this.Projectile;
        p.width = p.height = 10;
        p.aiStyle = 0;
        p.tileCollide = false;
        p.timeLeft = 600;
        p.friendly = true;
    }
    ShouldUpdatePosition(proj) { return false; }
}

export class Mover extends Frozen {
    ShouldUpdatePosition(proj) { return true; }
}

export class FrozenArrows extends GlobalProjectile {
    ShouldUpdatePosition(projectile) { return projectile.type !== ProjectileID.WoodenArrowFriendly || !freezeArrows; }
}
let freezeArrows = false;

const newProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const fire = (type, vx, vy, dy = 0) => {
    const c = me().Center;
    return Main.projectile[newProjectile(null, c.X, c.Y + dy, vx, vy, type, 0, 0, Main.myPlayer, 0, 0, 0, null)];
};
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const area = () => ({ x: Main.spawnTileX + 120, floor: Main.spawnTileY - 40, ceiling: Main.spawnTileY - 52 });
const P = (cls) => ModContent.ProjectileType(cls);

function clear() {
    const { x, floor, ceiling } = area();
    for (let i = x - 30; i <= x + 30; i++) for (const j of [floor, ceiling]) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
}

function stand() {
    const p = me(), { x, floor } = area();
    p.position = Vector2.new(x * 16 - p.width / 2, floor * 16 - p.height);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = floor - 3;
}

function killAll(...types) {
    for (let k = 0; k < 1000; k++) {
        const q = Main.projectile[k];
        if (q.active && types.includes(q.type)) q['void Kill()']();
    }
}

const latched = (type) => bl.projectiles.latchedHooks(Main.myPlayer, -1).filter((h) => h.type === type);

const S = 400;   // o primeiro quadro deste teste
let frames = 0, done = false, standing = true;
const state = {};
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    frames++;
    if (frames < S) return;
    const f = frames - S;

    if (f === 0) {
        clear();
        const { x, floor, ceiling } = area();
        for (let k = x - 30; k <= x + 30; k++) {
            placeTile(k, floor, TileID.Stone);
            placeTile(k, ceiling, TileID.Stone);
        }
    }
    if (standing) stand();

    // Alcance e volta: no ar, para os lados (sem bloco para prender).
    if (f === 5) {
        fire(P(ShortHook), 10, 0, -8);
        fire(P(PlainHook), -10, 0, -8);
    }
    if (f === 80) {
        const s = retract.get('short'), p = retract.get('plain');
        check('GrappleRange 100: volta perto de 100 px', () => (s && s.at >= 95 && s.at <= 125) || JSON.stringify(s));
        check('sem GrappleRange: volta perto de 300 px (o padrão do tModLoader)', () => (p && p.at >= 295 && p.at <= 325) || JSON.stringify(p));
        check('GrappleRetreatSpeed 30: a volta corre a 30', () => (s && Math.abs(s.speed - 30) < 0.5) || JSON.stringify(s));
        check('sem GrappleRetreatSpeed: a volta do jogo (11)', () => (p && Math.abs(p.speed - 11) < 0.5) || JSON.stringify(p));
        killAll(P(ShortHook), P(PlainHook));
    }

    // Cinco presos no teto, um a cada 6 quadros; depois o sexto.
    if (f >= 90 && f <= 114 && (f - 90) % 6 === 0) {
        const n = (f - 90) / 6;
        const hook = fire(P(ManyHook), (n - 2) * 2, -14);
        if (n === 0) state.first = hook.whoAmI;
    }
    if (f === 150) {
        check('NumGrappleHooks 5: cinco presos (o jogo deixaria 3)', () => latched(P(ManyHook)).length === 5 || `${latched(P(ManyHook)).length} presos`);
        fire(P(ManyHook), 6, -14);
    }
    if (f === 190) {
        const hooks = latched(P(ManyHook));
        check('o sexto solta o mais velho', () =>
            (hooks.length === 5 && !hooks.some((h) => h.whoAmI === state.first)) || `${hooks.length} presos, o primeiro ${hooks.some((h) => h.whoAmI === state.first) ? 'ainda preso' : 'solto'}`);
        killAll(P(ManyHook));
    }

    // O puxão: o jogador solto, com um gancho lento preso no teto.
    if (f === 200) state.slow = fire(P(SlowHook), 0, -14);
    if (f === 215) standing = false;
    if (f > 215 && f <= 245 && state.slow.active && state.slow.ai[0] === 2) {
        pulls.push({ x: self.velocity.X, speed: Math.hypot(self.velocity.X, self.velocity.Y) });
    }
    if (f === 250) {
        const top = pulls.reduce((m, v) => Math.max(m, v.speed), 0);
        check('GrapplePullSpeed 3: o puxão não passa de 3', () => (pulls.length > 5 && top <= 3.05 && top > 1) || `${pulls.length} quadros, máximo ${top}`);
        check('GrappleTargetPoint +200 em X: o puxão vai para a direita', () => pulls.some((v) => v.x > 1) || JSON.stringify(pulls.slice(0, 5)));
        killAll(P(SlowHook));
        standing = true;
    }

    // ShouldUpdatePosition.
    if (f === 260) {
        freezeArrows = true;
        state.frozen = fire(P(Frozen), 5, 0, -40);
        state.mover = fire(P(Mover), 5, 0, -60);
        state.arrow = fire(ProjectileID.WoodenArrowFriendly, 5, 0, -80);
        state.start = { frozen: state.frozen.position.X, mover: state.mover.position.X, arrow: state.arrow.position.X, arrowY: state.arrow.position.Y };
    }
    if (f === 280) {
        const { frozen, mover, arrow, start } = state;
        check('ShouldUpdatePosition false: o de mod fica no lugar, com velocidade', () =>
            (Math.abs(frozen.position.X - start.frozen) < 0.5 && frozen.velocity.X === 5) || `x ${start.frozen} -> ${frozen.position.X}, vx ${frozen.velocity.X}`);
        check('ShouldUpdatePosition true: o de controle anda', () => mover.position.X - start.mover > 80 || `x ${start.mover} -> ${mover.position.X}`);
        check('GlobalProjectile: a flecha do jogo fica no lugar', () =>
            (arrow.active && Math.abs(arrow.position.X - start.arrow) < 0.5 && Math.abs(arrow.position.Y - start.arrowY) < 0.5) || `ativa ${arrow.active}, x ${start.arrow} -> ${arrow.position.X}`);
        freezeArrows = false;
        killAll(P(Frozen), P(Mover), ProjectileID.WoodenArrowFriendly);
        clear();
        const p = me();
        p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
        p.fallStart = Main.spawnTileY - 3;
        done = true;
        bl.log('projgrapple FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('projgrapple: carregado');

export default class TestProjGrapple extends Mod {}
