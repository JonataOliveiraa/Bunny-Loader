// O OnSpawn das nuvens de mod: toda nuvem de mod que aparece no céu passou
// pelo OnSpawn? O OnSpawn daqui marca a nuvem com spriteDir = FlipVertically
// (o jogo só sorteia None e FlipHorizontally, e o UpdateClouds, que reordena
// as nuvens clonando-as entre as vagas, copia a marca). Casos: o addCloud
// chamado à mão (comum, e rara com o mundo de aniversário ligado para ela
// sair 1 em 3) e o céu andando sozinho por 10 s, que é o caminho real.
// Loga "cloudspawn <caso>: ok | FALHOU".
const Main = Terraria.Main;
const MARK = 2;   // SpriteEffects.FlipVertically

let commonWeight = 0, rareWeight = 0, onSpawns = 0;

export class TestSpawnCloud extends ModCloud {
    Texture = 'Textures/TestCloud';
    SpawnChance() { return commonWeight; }
    OnSpawn(cloud) { onSpawns++; cloud.spriteDir = MARK; }
}

export class TestSpawnRareCloud extends ModCloud {
    Texture = 'Textures/TestCloud';
    get RareCloud() { return true; }
    SpawnChance() { return rareWeight; }
    OnSpawn(cloud) { onSpawns++; cloud.spriteDir = MARK; }
}

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true) bl.log('cloudspawn ' + label + ': ok');
        else { fails++; bl.log('cloudspawn ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('cloudspawn ' + label + ': FALHOU com ' + e);
    }
}

// As nuvens de mod vivas, e quantas delas sem a marca do OnSpawn.
function scan() {
    const common = ModContent.GetInstance(TestSpawnCloud).Type, rare = ModContent.GetInstance(TestSpawnRareCloud).Type;
    let mod = 0, missing = 0;
    for (let i = 0; i < Main.cloud.length; i++) {
        const c = Main.cloud[i];
        if (!c.active || (c.type !== common && c.type !== rare)) continue;
        mod++;
        if (c.spriteDir !== MARK) missing++;
    }
    return { mod, missing };
}

function clear() {
    for (let i = 0; i < Main.cloud.length; i++) Main.cloud[i].active = false;
}

let frames = 0, phase = 0, natural = { mod: 0, missing: 0 }, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (phase === 0 && frames === 60) {
        const anniversary = Main.tenthAnniversaryWorld;
        check('comum no addCloud: toda nuvem de mod com OnSpawn', () => {
            // O peso das comuns é relido a cada 60 tentativas: gasta as do cache.
            commonWeight = 1e6;
            for (let k = 0; k < 61; k++) Terraria.Cloud.addCloud();
            clear();
            for (let k = 0; k < 40; k++) Terraria.Cloud.addCloud();
            commonWeight = 0;
            const s = scan();
            return (s.mod > 0 && s.missing === 0) || `${s.mod} de mod, ${s.missing} sem OnSpawn`;
        });
        check('rara no addCloud: toda nuvem de mod com OnSpawn', () => {
            clear();
            rareWeight = 1e6;
            Main.tenthAnniversaryWorld = true;
            for (let k = 0; k < 200; k++) Terraria.Cloud.addCloud();
            Main.tenthAnniversaryWorld = anniversary;
            rareWeight = 0;
            const s = scan();
            return (s.mod > 0 && s.missing === 0) || `${s.mod} de mod, ${s.missing} sem OnSpawn`;
        });
        // O céu sozinho: as duas com peso, o mundo de aniversário ligado.
        clear();
        commonWeight = 5;
        rareWeight = 1e6;
        Main.tenthAnniversaryWorld = true;
        Main.numClouds = 200;
        phase = 2;
        frames = 0;
        return;
    }
    if (phase === 2) {
        const s = scan();
        natural.mod = Math.max(natural.mod, s.mod);
        natural.missing = Math.max(natural.missing, s.missing);
        if (frames < 600) return;
        Main.tenthAnniversaryWorld = false;
        commonWeight = rareWeight = 0;
        check('céu andando sozinho: toda nuvem de mod com OnSpawn', () =>
            (natural.mod > 0 && natural.missing === 0) || `até ${natural.mod} de mod, ${natural.missing} sem OnSpawn (${onSpawns} OnSpawn)`);
        done = true;
        bl.log('cloudspawn FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});

bl.log('cloudspawn: carregado');

export default class TestCloudSpawn extends Mod {}
