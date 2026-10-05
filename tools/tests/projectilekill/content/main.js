const Main = Terraria.Main;
const Pr = Terraria.Projectile;
const counts = new Map(), spawned = new Map(), placed = [];
const types = new Map();
let frames = 0, started = -1, done = false, failures = 0, floor = 0, target = null;
let collisionKill = false, hitResult = null;
const newProjectile = Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const newNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];
const placeTile = Terraria.WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'];
const killTile = Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];

function record(name, event) {
    if (!counts.has(name)) counts.set(name, { tile: 0, pre: 0, kill: 0, global: 0, hit: 0, time: -1 });
    const state = counts.get(name);
    state[event]++;
    return state;
}
function check(name, run) {
    try {
        if (!run()) throw new Error('resultado falso');
        bl.log('projectilekill ' + name + ': ok');
    } catch (error) { failures++; bl.log('projectilekill ' + name + ': FALHOU ' + error); }
}

class GroundProbe extends ModProjectile {
    Texture = 'Box';
    SetDefaults(p) {
        p.width = p.height = 10;
        p.aiStyle = -1;
        p.tileCollide = true;
        p.timeLeft = 360;
        p.penetrate = -1;
        p.ignoreWater = true;
    }
    AI(p) { p.velocity.Y = Math.min(p.velocity.Y + 0.5, 8); }
    OnTileCollide(p, oldVelocity) { record(this.constructor.name, 'tile'); return false; }
    PreKill(p, time) { record(this.constructor.name, 'pre'); return true; }
    OnKill(p, time) { record(this.constructor.name, 'kill').time = time; }
    OnHitNPC(p, npc) { record(this.constructor.name, 'hit'); }
}

export class TimeoutGround extends GroundProbe {}
export class HitGround extends GroundProbe {
    SetDefaults(p) { super.SetDefaults(p); p.friendly = true; p.penetrate = 1; }
}
export class KillFromCollision extends GroundProbe {
    OnTileCollide(p, oldVelocity) {
        record(this.constructor.name, 'tile');
        if (collisionKill) p['void Kill()']();
        return false;
    }
}

export class GlobalKillProbe extends GlobalProjectile {
    AppliesToEntity(p) { return types.has(p.type); }
    OnKill(p, time) { record(types.get(p.type), 'global'); }
}

function spawn(name, x) {
    const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const type = ModProjectile.getTypeByName(name);
    types.set(type, name);
    const index = newProjectile(source, x, floor - 40, 0, 1, type, 20, 0, Main.myPlayer, 0, 0, 0, null);
    if (index < 0 || index >= Main.projectile.length) throw new Error('sem slot de projetil');
    spawned.set(name, Main.projectile[index]);
}

function prepare(player) {
    const x = Math.floor(player.Center.X / 16), head = Math.floor(player.position.Y / 16);
    let y = head - 8;
    while (y > head - 35 && solid(Vector2.new((x - 6) * 16, (y - 5) * 16), 13 * 16, 7 * 16)) y--;
    if (y <= head - 35) throw new Error('sem faixa livre para teste');
    floor = y * 16;
    for (let column = x - 6; column <= x + 6; column++) {
        if (!placeTile(column, y, Terraria.ID.TileID.Dirt, true, true, -1, 0)) throw new Error('nao criou o piso');
        placed.push([column, y]);
    }
    spawn('TimeoutGround', (x - 4) * 16);
    spawn('HitGround', x * 16);
    spawn('KillFromCollision', (x + 4) * 16);
}

const updatePosition = Pr['void UpdatePosition(Vector2 wetVelocity)'];
updatePosition.hook((original, p, wet) => {
    original(p, wet);
    if (!done && spawned.get('TimeoutGround') && bl.addressOf(p) === bl.addressOf(spawned.get('TimeoutGround')) && p.timeLeft === 1) {
        p.position.Y = floor - p.height;
        p.velocity.Y = 0.5;
    }
});

let hitNow = false;
Pr['void AI()'].hook((original, p) => {
    original(p);
    const hit = spawned.get('HitGround');
    if (hitNow && hit && bl.addressOf(hit) === bl.addressOf(p) && p.active) {
        hitNow = false;
        p.position.Y = floor - p.height;
        p.velocity.Y = 0.5;
        target.Center = p.Center;
        target.immune[Main.myPlayer] = 0;
    }
});

Pr['void Update(int i)'].hook((original, p, index) => {
    original(p, index);
    const hit = spawned.get('HitGround'), state = counts.get('HitGround');
    if (!hitResult && hit && state && state.hit > 0 && bl.addressOf(hit) === bl.addressOf(p)) {
        hitResult = { inactive: !p.active, kills: state.kill, hits: state.hit, penetrate: p.penetrate };
        bl.log('projectilekill diag apos Update: ' + JSON.stringify(hitResult));
    }
});

function act() {
    for (const name of ['TimeoutGround', 'HitGround', 'KillFromCollision']) {
        const p = spawned.get(name);
        check(name + ' sobreviveu ao bloco', () => p.active && counts.get(name)?.tile > 0 && counts.get(name).global === 0);
    }
    const timeout = spawned.get('TimeoutGround');
    timeout.timeLeft = 1;
    collisionKill = true;
    const hit = spawned.get('HitGround'), source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const index = newNPC(source, Math.floor(hit.Center.X), Math.floor(hit.Center.Y), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
    if (index < 0 || index >= Main.npc.length) throw new Error('sem slot de NPC');
    target = Main.npc[index];
    target.lifeMax = target.life = 10000;
    target.damage = 0;
    hitNow = true;
}

function cleanup() {
    done = true;
    hitNow = false;
    for (const p of spawned.values()) p.active = false;
    if (target) target.active = false;
    for (const [x, y] of placed) killTile(x, y, false, false, true);
}

Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Main.gameMenu || index !== Main.myPlayer || ++frames < 60) return;
    try {
        if (started < 0) { prepare(player); started = frames; return; }
        const elapsed = frames - started;
        if (elapsed === 40) act();
        if (elapsed === 50) {
            check('tempo no chao chama OnKill uma vez', () => !spawned.get('TimeoutGround').active && counts.get('TimeoutGround').kill === 1 && counts.get('TimeoutGround').time === 0);
            check('acerto em NPC chama OnKill uma vez', () => hitResult && hitResult.inactive && hitResult.hits > 0 && hitResult.kills === 1 && !spawned.get('HitGround').active && counts.get('HitGround').kill === 1);
            check('Kill dentro de OnTileCollide chama OnKill uma vez', () => !spawned.get('KillFromCollision').active && counts.get('KillFromCollision').kill === 1);
            check('GlobalProjectile.OnKill apenas na morte real', () => ['TimeoutGround', 'HitGround', 'KillFromCollision'].every((name) => counts.get(name).global === 1));
            for (const name of ['TimeoutGround', 'HitGround', 'KillFromCollision']) bl.log('projectilekill diag ' + name + ': ' + JSON.stringify(counts.get(name)) + ' ativo=' + spawned.get(name).active);
            cleanup();
            bl.log('projectilekill FIM falhas=' + failures);
        }
    } catch (error) {
        failures++;
        bl.log('projectilekill preparo: FALHOU ' + error);
        cleanup();
        bl.log('projectilekill FIM falhas=' + failures);
    }
});

export default class ProjectileKillTest extends Mod {}
