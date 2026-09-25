// Etapa 1 do Example Mod: confere cada item pelos numeros que o jogo ve, e o
// tiro da espingarda (8 projeteis) e do lanca-foguetes (o foguete da municao).
// Precisa do Example Mod ligado. Loga "exmod1 <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID, ProjectileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('exmod1 ' + label + ': ok');
        else { fails++; bl.log('exmod1 ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('exmod1 ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// Os tipos do Example Mod, pelo nome que o jogo mostra (typeOf so ve os deste mod).
function itemType(name) {
    for (let t = bl.items.vanillaCount; t < bl.items.vanillaCount + 200; t++) {
        if (!bl.items.isModItem(t)) break;
        if (Terraria.Lang['string GetItemNameValue(int id)'](t) === name) return t;
    }
    return -1;
}

function sample(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
}

function countProjectiles(type) {
    let n = 0;
    const all = Main.projectile;
    for (let i = 0; i < 1000; i++) if (all[i].active && all[i].type === type) n++;
    return n;
}

function killProjectiles(type) {
    const all = Main.projectile;
    for (let i = 0; i < 1000; i++) if (all[i].active && all[i].type === type) all[i].active = false;
}

const me = () => Main.player[Main.myPlayer];

// Arma no slot 9 e municao no 54, como o jogador teria. Devolve o que estava la.
function equip(weapon, ammoType) {
    const p = me();
    const saved = [9, 54].map((s) => ({ s, type: p.inventory[s].type, stack: p.inventory[s].stack }));
    p.inventory[9]['void SetDefaults(int Type, ItemVariant variant)'](weapon, null);
    p.inventory[54]['void SetDefaults(int Type, ItemVariant variant)'](ammoType, null);
    p.inventory[54].stack = 50;
    return saved;
}

function restore(saved) {
    const p = me();
    for (const { s, type, stack } of saved) {
        p.inventory[s]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        p.inventory[s].stack = stack;
    }
}

function shoot(weapon) {
    const p = me();
    p['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](
        Main.myPlayer, p.inventory[9], weapon.damage, false);
}

function run() {
    const soul = itemType('Alma de Exemplo');
    const pick = itemType('Picareta de Exemplo');
    const hamaxe = itemType('Martelo-Machado de Exemplo');
    const ball = itemType('Bola de Golfe de Exemplo');
    const shotgun = itemType('Espingarda de Exemplo');
    const launcher = itemType('Lança-Foguetes de Exemplo');
    bl.log(`exmod1: alma ${soul}, picareta ${pick}, martelo ${hamaxe}, bola ${ball}, espingarda ${shotgun}, lanca ${launcher}`);

    check('ajudantes globais', () => {
        const v = Vector2.RotatedBy(Vector2.new(1, 0), MathHelper.PiOver2);
        if (Math.abs(v.X) > 1e-4 || Math.abs(v.Y - 1) > 1e-4) return 'RotatedBy deu ' + v;
        if (ItemRarityID.Pink !== 5 || ProjAIStyleID.GolfBall !== 149) return 'IDs errados';
        const c = Color.SkyBlue;
        if (c.R !== 135 || c.B !== 235) return 'Color.SkyBlue = ' + c;
        const r = Rand.Next(10);
        return (r >= 0 && r < 10) || 'Rand.Next(10) = ' + r;
    });

    check('alma', () => {
        const it = sample(soul);
        if (!it.maxStack || it.maxStack < 999) return 'maxStack ' + it.maxStack;
        if (it.rare !== ItemRarityID.Pink) return 'rare ' + it.rare;
        if (!ItemID.Sets.ItemNoGravity[soul]) return 'sem ItemNoGravity';
        if (!ItemID.Sets.AnimatesAsSoul[soul]) return 'sem AnimatesAsSoul';
        const anim = Main.itemAnimations[soul];
        return (anim && anim.FrameCount === 4) || 'animacao ' + (anim ? anim.FrameCount : 'nula');
    });

    check('picareta', () => {
        const it = sample(pick);
        return (it.pick === 220 && it.damage === 20 && it.melee) || `pick ${it.pick} dano ${it.damage}`;
    });

    check('martelo-machado', () => {
        const it = sample(hamaxe);
        return (it.axe === 30 && it.hammer === 100) || `axe ${it.axe} hammer ${it.hammer}`;
    });

    check('bola de golfe', () => {
        const it = sample(ball);
        if (it.shoot < bl.projectiles.vanillaCount) return 'shoot ' + it.shoot;
        if (!ProjectileID.Sets.IsAGolfBall[it.shoot]) return 'IsAGolfBall falso';
        return ProjectileID.Sets.TrailCacheLength[it.shoot] === 20 || 'TrailCacheLength ' + ProjectileID.Sets.TrailCacheLength[it.shoot];
    });

    check('espingarda: 8 projeteis', () => {
        const gun = sample(shotgun);
        const saved = equip(shotgun, ItemID.MusketBall);
        killProjectiles(ProjectileID.Bullet);
        shoot(gun);
        const n = countProjectiles(ProjectileID.Bullet);
        killProjectiles(ProjectileID.Bullet);
        restore(saved);
        return n === 8 || n + ' projetil(eis)';
    });

    check('lanca-foguetes: foguete da municao', () => {
        const gun = sample(launcher);
        const saved = equip(launcher, ItemID.RocketI);
        killProjectiles(ProjectileID.RocketI);
        shoot(gun);
        const n = countProjectiles(ProjectileID.RocketI);
        killProjectiles(ProjectileID.RocketI);
        restore(saved);
        return n === 1 || n + ' foguete(s)';
    });

    bl.log('exmod1 FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 90) run();
});
bl.log('exmod1: carregado');
