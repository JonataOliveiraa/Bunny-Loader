// Teste dos projeteis de mod (runtime/ModProjectiles.cpp). Precisa do Example
// Mod ligado: ele registra o projetil ExampleBulletProjectile, o primeiro de
// mod (tipo 1111 = ProjectileID.Count), e a arma e a bala.
//
// Cria o projetil perto do jogador e confere: nasce ativo, com o tipo e o
// setDefaults do mod; anda; conta no ownedProjectileCounts do dono (a tabela
// por JOGADOR que precisou crescer). Depois poe a arma e 999 balas em espacos
// vazios do inventario, para o teste de tiro na mao.
// Loga "projeteis <caso>: ok | FALHOU".
const Main = Terraria.Main;
const PROJ = 1111;
const GUN_NAME = /Arma de Exemplo|Example Gun/;
const BULLET_NAME = /Bala de Exemplo|Example Bullet/;
const itemName = Terraria.Lang['string GetItemNameValue(int id)'];
const newProjectile = Terraria.Projectile[
    'int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, ' +
    'int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, ' +
    'NewProjectileModifier modifer)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('projeteis ' + label + ': ok');
        else { fails++; bl.log('projeteis ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('projeteis ' + label + ': FALHOU com ' + e);
    }
}

let proj = null, startX = 0, startY = 0;

function spawn() {
    check('registrado', () => bl.projectiles.isModProjectile(PROJ) || 'isModProjectile(1111) = false');
    check('tabelas', () => {
        if (Main.projFrames.length <= PROJ) return 'projFrames ' + Main.projFrames.length;
        const tex = Terraria.GameContent.TextureAssets.Projectile;
        if (tex.length <= PROJ || tex[PROJ] === null) return 'TextureAssets.Projectile';
        const owned = Main.player[Main.myPlayer].ownedProjectileCounts;
        if (owned.length <= PROJ) return 'ownedProjectileCounts ' + owned.length;
    });
    check('nome', () => {
        const n = Terraria.Lang['LocalizedText GetProjectileName(int type)'](PROJ).Value;
        return BULLET_NAME.test(n) || 'nome=' + n;
    });
    check('nasce', () => {
        const p = Main.player[Main.myPlayer];
        const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
        source['void .ctor()']();
        startX = p.position.X;
        startY = p.position.Y - 60;
        // Controle: a bala comum do jogo (14), pelo mesmo caminho.
        const c = Main.projectile[newProjectile(source, startX, startY - 30, 6, 0, 14, 10, 1, Main.myPlayer, 0, 0, 0, null)];
        bl.log(`projeteis controle (14): ativo=${c.active} aiStyle=${c.aiStyle} timeLeft=${c.timeLeft} width=${c.width}`);
        const i = newProjectile(source, startX, startY, 6, 0, PROJ, 10, 1, Main.myPlayer, 0, 0, 0, null);
        proj = Main.projectile[i];
        bl.log(`projeteis estado (${PROJ}) em [${i}]: ativo=${proj.active} aiStyle=${proj.aiStyle} ` +
               `timeLeft=${proj.timeLeft} width=${proj.width} penetrate=${proj.penetrate} owner=${proj.owner}`);
        if (!proj.active || proj.type !== PROJ) return `projectile[${i}] ativo=${proj.active} tipo=${proj.type}`;
        // Os valores do setDefaults do mod (os do original sao zeros).
        if (proj.aiStyle !== 1 || proj.penetrate !== 5 || proj.width !== 8 || !proj.friendly) {
            return `aiStyle=${proj.aiStyle} penetrate=${proj.penetrate} width=${proj.width} friendly=${proj.friendly}`;
        }
    });
}

function afterFrames() {
    check('anda', () => {
        if (!proj) return 'nao nasceu';
        const dx = proj.position.X - startX;
        return (dx > 10) || `andou ${dx.toFixed(1)} px (ativo=${proj.active})`;
    });
    check('conta no dono', () => {
        const n = Main.player[Main.myPlayer].ownedProjectileCounts[PROJ];
        return n >= 1 || 'ownedProjectileCounts[1111]=' + n;
    });
    check('arma e balas no inventario', () => {
        let gun = -1, bullet = -1;
        for (let t = 6147; t < 6147 + 64; t++) {
            const n = itemName(t);
            if (gun < 0 && GUN_NAME.test(n)) gun = t;
            if (bullet < 0 && BULLET_NAME.test(n)) bullet = t;
        }
        if (gun < 0 || bullet < 0) return `arma=${gun} bala=${bullet}`;
        const inv = Main.player[Main.myPlayer].inventory;
        let placed = 0;
        for (let s = 10; s < 50 && placed < 2; s++) {
            if (inv[s].type !== 0) continue;
            inv[s]['void SetDefaults(int Type, ItemVariant variant)'](placed === 0 ? gun : bullet, null);
            if (placed === 1) inv[s].stack = 999;
            placed++;
        }
        return placed === 2 || 'sem espaco vazio';
    });
    bl.log('projeteis FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 60) spawn();
    if (frames === 75) afterFrames();
});
bl.log('projeteis: carregado');
