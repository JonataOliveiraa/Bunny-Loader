// O spawn natural como no tModLoader (Loaders/SpawnLoader.js):
//   - EditSpawnRate: chamado com o jogador-alvo, e a taxa dele vale (forçada
//     a 1 aqui, o spawn acontece em poucas tentativas);
//   - EditSpawnInfo antes do sorteio, no mesmo ponto; SpawnChance recebe o
//     ponto, o bloco do chão, o jogador e os campos do NPC.Spawner;
//   - EditSpawnPool: só o de mod (tirando o 0), nada (pool vazio) e um tipo
//     do jogo no lugar do sorteio dele; GlobalNPC.SpawnNPC depois;
//   - EditSpawnRange: a área e a distância segura do GetSpawnArea;
//   - sem mudanças, o spawn do jogo continua.
// Loga "modspawn <caso>: ok | FALHOU".
const Main = Terraria.Main;
const N = Terraria.NPC;
const { NPCID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modspawn ' + label + ': ok');
        else { fails++; bl.log('modspawn ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modspawn ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// O que o teste liga em cada caso.
const state = { forceRate: false, pool: 'off', weight: 0, range: 'off' };
// O que os ganchos viram.
const seen = { seq: 0, rate: [], info: [], pool: [], chance: [], spawned: [] };

export class SpawnBlob extends ModNPC {
    SetDefaults() {
        this.NPC.width = 24;
        this.NPC.height = 18;
        this.NPC.aiStyle = 0;
        this.NPC.damage = 0;
        this.NPC.lifeMax = 50;
        this.NPC.noGravity = true;
    }
    SpawnChance(info) {
        seen.chance.push({
            player: info.Player ? info.Player.whoAmI : -1, x: info.SpawnTileX, y: info.SpawnTileY,
            ground: info.GroundTileY, tile: info.SpawnTileType, wall: info.SpawnWallType,
            water: info.waterTile, spawner: !!info.Spawner, surface: info.AboveSurface,
        });
        return state.weight;
    }
}

export class SpawnGlobal extends GlobalNPC {
    EditSpawnRate(player, spawnRate, maxSpawns) {
        seen.rate.push({ player: player.whoAmI, rate: spawnRate.value, max: maxSpawns.value });
        if (seen.rate.length > 50) seen.rate.shift();
        if (state.forceRate) { spawnRate.value = 1; maxSpawns.value = 200; }
    }
    EditSpawnRange(player, rangeX, rangeY, safeX, safeY) {
        if (state.range === 'small') { rangeX.value = 30; rangeY.value = 20; safeX.value = 10; safeY.value = 8; }
    }
    EditSpawnInfo(info) {
        seen.info.push({ seq: ++seen.seq, x: info.SpawnTileX, y: info.SpawnTileY, player: info.Player.whoAmI });
    }
    EditSpawnPool(pool, info) {
        seen.pool.push({ seq: ++seen.seq, x: info.SpawnTileX, y: info.SpawnTileY, keys: pool.Keys });
        if (state.pool === 'mod') pool.Remove(0);
        else if (state.pool === 'none') pool.Clear();
        else if (state.pool === 'vanilla') { pool.Clear(); pool[NPCID.BlueSlime] = 1; }
    }
    SpawnNPC(npc, tileX, tileY) {
        seen.spawned.push({ index: npc, x: tileX, y: tileY });
    }
}

const blobType = () => ModContent.NPCType('SpawnBlob');
const hostiles = () => {
    const out = [];
    for (let i = 0; i < 200; i++) {
        const n = Main.npc[i];
        if (n.active && !n.townNPC && !n.friendly) out.push(i);
    }
    return out;
};
const clearHostiles = () => { for (const i of hostiles()) Main.npc[i].active = false; };
const reset = () => { seen.rate = []; seen.info = []; seen.pool = []; seen.chance = []; seen.spawned = []; };
const spawnNPC = N['void SpawnNPC()'];
// Tenta até `until()` ou o limite; devolve quantas tentativas.
function tries(limit, until) {
    let k = 0;
    for (; k < limit && !until(); k++) spawnNPC();
    return k;
}

function rateCase() {
    reset();
    state.forceRate = true;
    spawnNPC();
    check('EditSpawnRate com o jogador-alvo e a taxa do jogo', () => {
        const r = seen.rate[0];
        return (r && r.player === Main.myPlayer && r.rate > 1 && r.max > 0 && Number.isInteger(r.rate)) || JSON.stringify(seen.rate.slice(0, 2));
    });
}

function modOnlyCase() {
    clearHostiles();
    reset();
    state.pool = 'mod';
    state.weight = 1;
    const k = tries(3000, () => seen.spawned.length > 0);
    const found = hostiles().map((i) => Main.npc[i].type);
    check('só o de mod no sorteio: nasce ele, e nada do jogo', () =>
        (seen.spawned.length > 0 && found.length > 0 && found.every((t) => t === blobType())) ||
        `tentativas ${k}, nascidos ${JSON.stringify(seen.spawned)}, na tela ${found}`);
    check('o ModNPC nasce em cima do bloco do spawn (SpawnNPC(tileX, tileY))', () => {
        const s = seen.spawned[0];
        const npc = s && Main.npc[s.index];
        return (npc && npc.type === blobType() && Math.abs(npc.Bottom.Y - s.y * 16) < 1 && Math.abs(npc.Center.X - (s.x * 16 + 8)) < 1) ||
            JSON.stringify(s) + (npc ? ` npc ${npc.type} em ${npc.Center.X},${npc.Bottom.Y}` : '');
    });
    check('SpawnChance: o ponto, o chão, o bloco, o jogador e os campos do NPC.Spawner', () => {
        const c = seen.chance[seen.chance.length - 1];
        if (!c) return 'SpawnChance não foi chamado';
        const tile = bl.tiles.typeAt(c.x, c.ground);
        return (c.player === Main.myPlayer && c.ground >= c.y && c.tile === tile && c.spawner && typeof c.water === 'boolean') ||
            JSON.stringify(c) + ' bloco no chão ' + tile;
    });
    check('EditSpawnInfo antes do EditSpawnPool, no mesmo ponto', () => {
        const p = seen.pool[seen.pool.length - 1];
        const i = p && seen.info.filter((x) => x.seq < p.seq).pop();
        return (p && i && i.x === p.x && i.y === p.y && i.player === Main.myPlayer && p.keys.includes(0) && p.keys.includes(blobType())) ||
            JSON.stringify({ info: seen.info.slice(-2), pool: seen.pool.slice(-2) });
    });
}

function noneCase() {
    clearHostiles();
    reset();
    state.pool = 'none';
    tries(400, () => false);
    check('pool vazio: nada nasce', () =>
        (seen.pool.length > 0 && hostiles().length === 0 && seen.spawned.length === 0) ||
        `sorteios ${seen.pool.length}, na tela ${hostiles().map((i) => Main.npc[i].type)}`);
}

function vanillaTypeCase() {
    clearHostiles();
    reset();
    state.pool = 'vanilla';
    const k = tries(3000, () => seen.spawned.length > 0);
    check('um tipo do jogo no pool: nasce ele, com o GlobalNPC.SpawnNPC', () => {
        const s = seen.spawned[0];
        const npc = s && Main.npc[s.index];
        return (npc && npc.active && npc.type === NPCID.BlueSlime) || `tentativas ${k}, ${JSON.stringify(seen.spawned)}`;
    });
}

function rangeCase() {
    const p = Main.player[Main.myPlayer];
    const area = N.Spawner['void GetSpawnArea(Player player, out Rectangle spawnArea, out Rectangle safeArea)'];
    const a = new Ref(Rectangle.new(0, 0, 0, 0)), s = new Ref(Rectangle.new(0, 0, 0, 0));

    state.range = 'off';
    area(p, a, s);
    const w = Math.trunc(N.sWidth / 16), h = Math.trunc(N.sHeight / 16);
    check('EditSpawnRange sem mudar: a área do jogo', () =>
        (a.value.Width === Math.trunc(w * 0.7) * 2 && s.value.Width === Math.trunc(w * 0.52) * 2 && N.safeRangeY === Math.trunc(h * 0.52)) ||
        `área ${a.value.Width}x${a.value.Height}, segura ${s.value.Width}x${s.value.Height}, tela ${w}x${h}`);

    state.range = 'small';
    area(p, a, s);
    const cx = Math.trunc(p.position.X / 16), cy = Math.trunc(p.position.Y / 16);
    check('EditSpawnRange: a área e a distância segura novas, em volta do jogador', () =>
        (a.value.Width === 60 && a.value.Height === 40 && a.value.X === cx - 30 && a.value.Y === cy - 20 &&
         s.value.Width === 20 && s.value.Height === 16 && N.safeRangeX === 10 && N.safeRangeY === 8) ||
        `área ${a.value.X},${a.value.Y} ${a.value.Width}x${a.value.Height}, segura ${s.value.Width}x${s.value.Height}, jogador ${cx},${cy}`);
    state.range = 'off';
}

function vanillaCase() {
    clearHostiles();
    reset();
    state.pool = 'off';
    state.weight = 0;
    const k = tries(3000, () => hostiles().length > 0);
    check('sem mudanças no pool: o spawn do jogo continua', () =>
        (hostiles().length > 0 && hostiles().every((i) => Main.npc[i].type !== blobType()) && seen.spawned.length === 0) ||
        `tentativas ${k}, na tela ${hostiles().map((i) => Main.npc[i].type)}`);
}

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    self.statLife = self.statLifeMax2;
    self.immune = true;
    self.immuneTime = 30;
    if (frame === 1) { Main.dayTime = true; Main.time = 27000; }

    const steps = { 30: rateCase, 40: modOnlyCase, 50: noneCase, 60: vanillaTypeCase, 70: rangeCase, 80: vanillaCase };
    if (steps[frame]) check('passo ' + frame, steps[frame]);
    if (frame === 90) {
        state.forceRate = false;
        state.pool = 'off';
        clearHostiles();
        done = true;
        bl.log('modspawn FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modspawn: carregado');

export default class TestModSpawn extends Mod {}
