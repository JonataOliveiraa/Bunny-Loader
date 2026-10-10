// O lag do lacaio do Consolaria (EyeOfEternity atirando EternalLaser, que
// clona o EyeLaser e usa AIType = PurpleLaser). Fases de 300 quadros com
// lasers disparados num ritmo fixo e vida curta (o mesmo número vivo em cada
// fase): nenhum, laser do jogo, o do mod com AIType, o do mod sem AIType e,
// no fim, os lacaios de verdade atirando em bonecos de treino. Loga o tempo
// de DoUpdate/DoDraw (os primeiros 60 quadros à parte: "no começo caiu muito")
// e os hooks que mais gastam (bl.hookStats).
const Main = Terraria.Main;
const { ProjectileID, NPCID } = Terraria.ID;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const NewNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

const SETTLE = 240, LENGTH = 300, HEAD = 60, EVERY = 2, LIFE = 120, MINIONS = 10;
const PHASES = ['nada', 'jogo PurpleLaser', 'jogo EyeLaser', 'mod com AIType', 'mod sem AIType', 'lacaios'];

let laser = 0, minion = 0, buff = 0;
let phase = -1, frames = 0, settled = 0, done = false;
let update = [], draw = [], statsAt = null;
const spawned = [], dummies = [];

const me = () => Main.player[Main.myPlayer];
const source = () => Terraria.DataStructures.EntitySource_DebugCommand.new();

function snapshot() {
    const m = new Map();
    for (const h of bl.hookStats()) m.set(h.name, h);
    return m;
}

function summary(values) {
    const s = values.slice().sort((a, b) => a - b);
    if (!s.length) return '-';
    const avg = s.reduce((t, v) => t + v, 0) / s.length;
    return `${avg.toFixed(2)} ms (p90 ${s[Math.floor(s.length * 0.9)].toFixed(2)}, máx ${s[s.length - 1].toFixed(2)})`;
}

function report() {
    const name = PHASES[phase];
    const alive = Main.projectile.length;
    let count = 0;
    for (let i = 0; i < alive; i++) {
        const p = Main.projectile[i];
        if (p.active && p.type !== 0 && p.type !== minion) count++;
    }
    bl.log(`minionlaser ${name}: início DoUpdate ${summary(update.slice(0, HEAD))}, DoDraw ${summary(draw.slice(0, HEAD))}`);
    bl.log(`minionlaser ${name}: resto DoUpdate ${summary(update.slice(HEAD))}, DoDraw ${summary(draw.slice(HEAD))}, projéteis vivos ${count}`);

    const now = snapshot(), rows = [];
    for (const [hook, h] of now) {
        const before = statsAt.get(hook);
        const calls = (h.calls - (before ? before.calls : 0)) / frames;
        const js = (h.js - (before ? before.js : 0)) / frames;
        const ms = ((h.jsMs || 0) - (before ? before.jsMs || 0 : 0)) / frames;
        if (js > 0) rows.push({ hook: hook.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|System\.|Terraria\./g, ''), calls, js, ms });
    }
    rows.sort((a, b) => b.ms - a.ms);
    const total = rows.reduce((t, r) => t + r.ms, 0);
    bl.log(`minionlaser ${name}: JS nos hooks ${total.toFixed(2)} ms/quadro`);
    for (const r of rows.slice(0, 8)) {
        bl.log(`minionlaser ${name}:   ${r.ms.toFixed(3)} ms, ${r.js.toFixed(1)} entradas/${r.calls.toFixed(0)} chamadas: ${r.hook}`);
    }
}

function clear() {
    for (const p of spawned) if (p.active) p.Kill();
    spawned.length = 0;
    for (let i = 0; i < Main.projectile.length; i++) {
        const p = Main.projectile[i];
        if (p.active && (p.type === laser || p.type === minion)) p.Kill();
    }
}

function fire(type, noAIType) {
    const p0 = me();
    // Para o alto e de lado: nada no caminho, todos vivem LIFE quadros.
    const angle = -Math.PI / 2 + (Math.random() - 0.5) * 1.2;
    const vel = Vector2.new(Math.cos(angle) * 8, Math.sin(angle) * 8);
    const index = NewProjectile(source(), p0.Center, vel, type, 20, 1, Main.myPlayer, 0, 0, 0, null);
    const p = Main.projectile[index];
    p.friendly = true;
    p.hostile = false;
    p.timeLeft = LIFE * (p.extraUpdates + 1);
    if (noAIType && p.ModProjectile) p.ModProjectile.AIType = 0;
    spawned.push(p);
}

function start(next) {
    phase = next;
    frames = 0;
    update = [];
    draw = [];
    statsAt = snapshot();
    if (PHASES[phase] !== 'lacaios') return;

    const p0 = me();
    p0.AddBuff(buff, 36000, true, false);
    for (let i = 0; i < MINIONS; i++) {
        const index = NewProjectile(source(), p0.Center, Vector2.Zero, minion, 30, 1, Main.myPlayer, 0, 0, 0, null);
        spawned.push(Main.projectile[index]);
    }
    for (let i = 0; i < 3; i++) {
        const index = NewNPC(source(), Math.floor(p0.Center.X) + (12 + i * 4) * 16 * p0.direction,
            Math.floor(p0.position.Y), NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
        // Slime e não boneco de treino: o lacaio não mira o boneco.
        const n = Main.npc[index];
        n.lifeMax = n.life = 999999;
        n.damage = 0;
        dummies.push(n);
    }
}

Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    const t0 = performance.now();
    original(main, time);
    const t1 = performance.now();
    if (done || Main.gameMenu) return;

    if (phase < 0) {
        if (++settled < SETTLE) return;
        laser = ModContent.ProjectileType('consolaria/EternalLaser');
        minion = ModContent.ProjectileType('consolaria/EyeOfEternity');
        buff = ModContent.BuffType('consolaria/EyeOfEternity');
        bl.log(`minionlaser tipos: laser ${laser}, lacaio ${minion}, buff ${buff}`);
        if (!laser || !minion || !buff) {
            bl.log('minionlaser FIM: sem o Consolaria');
            done = true;
            return;
        }
        Main.dayTime = true;
        Main.time = 20000;
        start(0);
        return;
    }

    update.push(t1 - t0);
    const p0 = me();
    p0.statLife = p0.statLifeMax2;
    for (const n of dummies) if (n.active) n.life = n.lifeMax;
    const name = PHASES[phase];
    if (frames % EVERY === 0) {
        if (name === 'jogo PurpleLaser') fire(ProjectileID.PurpleLaser, false);
        else if (name === 'jogo EyeLaser') fire(ProjectileID.EyeLaser, false);
        else if (name === 'mod com AIType') fire(laser, false);
        else if (name === 'mod sem AIType') fire(laser, true);
    }
    if (++frames < LENGTH) return;

    report();
    clear();
    if (phase + 1 < PHASES.length) {
        start(phase + 1);
        return;
    }
    for (const n of dummies) if (n.active) n.active = false;
    p0.ClearBuff(buff);
    done = true;
    bl.log('minionlaser FIM');
});

// Os 10 lacaios ao mesmo tempo: o jogador sem acessório só sustenta 1.
Terraria.Player['void UpdateEquips(int i)'].hook((original, self, i) => {
    original(self, i);
    if (!done && PHASES[phase] === 'lacaios' && i === Main.myPlayer) self.maxMinions = MINIONS;
});

Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    const t0 = performance.now();
    original(main, time);
    if (phase >= 0 && !done && !Main.gameMenu) draw.push(performance.now() - t0);
});

bl.log('minionlaser: carregado');

export default class TestMinionLaser extends Mod {}
