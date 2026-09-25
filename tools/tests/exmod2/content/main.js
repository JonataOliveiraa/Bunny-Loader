// Etapa 2a do Example Mod e os ganchos novos de ModProjectile. Precisa do
// Example Mod ligado. Loga "exmod2 <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID, ProjectileID } = Terraria.ID;

const count = {};
const bump = (k) => { count[k] = (count[k] || 0) + 1; };

class TestBounce extends ModProjectile {
    SetDefaults() {
        this.Projectile.width = 10;
        this.Projectile.height = 10;
        this.Projectile.aiStyle = 1;
        this.Projectile.friendly = true;
        this.Projectile.damage = 1;
        this.Projectile.penetrate = -1;
        this.Projectile.timeLeft = 300;
        this.Projectile.tileCollide = true;
        this.AIType = ProjectileID.WoodenArrowFriendly;
    }
    OnSpawn() { bump('OnSpawn'); }
    AI(p) { if (p.type === this.Type) bump('AIType restaurado'); }
    OnTileCollide(p, oldVelocity) {
        bump('OnTileCollide');
        p.velocity.Y = -Math.abs(oldVelocity.Y);
        return false;
    }
    GetAlpha() { bump('GetAlpha'); return undefined; }
    PreDraw() { bump('PreDraw'); return true; }
    PostDraw() { bump('PostDraw'); }
    CanDamage() { bump('CanDamage'); return true; }
    ModifyDamageHitbox(p, hitbox) { bump('ModifyDamageHitbox'); hitbox.Width += 2; }
}
const BOUNCE = ModProjectile.register(TestBounce);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('exmod2 ' + label + ': ok');
        else { fails++; bl.log('exmod2 ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('exmod2 ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

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

const me = () => Main.player[Main.myPlayer];
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];

function countOf(type) {
    let n = 0;
    for (let i = 0; i < 1000; i++) { const p = Main.projectile[i]; if (p.active && p.type === type) n++; }
    return n;
}
function killAll(type) {
    for (let i = 0; i < 1000; i++) { const p = Main.projectile[i]; if (p.active && p.type === type) p.active = false; }
}

// Dispara a arma como o jogador: ela no slot 9, na mao, e o ItemCheck_Shoot.
function fire(type, keep = false) {
    const p = me();
    const it = p.inventory[9];
    const saved = { type: it.type, stack: it.stack };
    const savedSel = p.selectedItemState.selected;
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    p.selectedItemState.selected = 9;
    p.itemAnimationMax = 20;
    p.itemAnimation = 20;
    const shoot = it.shoot;
    killAll(shoot);
    p['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](Main.myPlayer, it, it.damage, false);
    const n = countOf(shoot);
    if (keep) return { shoot, n };
    p.selectedItemState.selected = savedSel;
    it['void SetDefaults(int Type, ItemVariant variant)'](saved.type, null);
    it.stack = saved.stack;
    return { shoot, n };
}

const WEAPONS = ['Lança de Exemplo', 'Mangual de Exemplo', 'Ioiô de Exemplo', 'Chicote de Exemplo',
                 'Espada de Energia de Exemplo', 'Cajado de Exemplo'];

function run() {
    check('makeGeneric e ProjAI', () => {
        const List = System.Collections.Generic.List.makeGeneric(Vector2.Type);
        const list = List.new();
        list['void .ctor()']();
        list.Add(Vector2.new(1, 2));
        list.Add(Vector2.new(3, 4));
        const arr = list.ToArray();
        if (arr.length !== 2 || arr[1].X !== 3) return 'List<Vector2> ' + arr.length;
        const Dict = System.Collections.Generic.Dictionary.makeGeneric(System.Int32, System.Int32);
        const d = Dict.new();
        d['void .ctor()']();
        d.Add(5, 7);
        if (d.Count !== 1) return 'Dictionary<int,int> Count ' + d.Count;
        const c = me().Center;
        const i = newProj(null, c.X, c.Y, 0, 0, ProjectileID.WoodenArrowFriendly, 0, 0, Main.myPlayer, 0, 0, 0, null);
        const pr = Main.projectile[i];
        const ai = new ProjAI(pr);
        ai[1] = 42;
        ai[1]++;
        const ok = pr.ai.val1 === 43;
        pr.active = false;
        return ok || 'ProjAI ' + pr.ai.val1;
    });

    for (const name of WEAPONS) {
        check('dispara: ' + name, () => {
            const t = itemType(name);
            if (t < 0) return 'item nao achado';
            const { shoot, n } = fire(t);
            return n >= 1 || `projetil ${shoot}: ${n}`;
        });
    }

    check('chicote: tabelas', () => {
        const t = itemType('Chicote de Exemplo');
        const it = sample(t);
        if (!ProjectileID.Sets.IsAWhip[it.shoot]) return 'IsAWhip falso';
        return !!ItemID.Sets.UniqueTagEffects[t] || 'sem UniqueTagEffects';
    });

    check('broca', () => {
        const t = itemType('Broca de Exemplo');
        const it = sample(t);
        return (it.pick === 190 && ItemID.Sets.IsDrill[t] && it.channel) || `pick ${it.pick}`;
    });

    const c = me().Center;
    newProj(null, c.X, c.Y - 48, 0, 6, BOUNCE, 1, 0, Main.myPlayer, 0, 0, 0, null);

    check('vara de pesca: boia', () => {
        const t = itemType('Vara de Pesca de Exemplo');
        if (!ItemID.Sets.CanFishInLava[t]) return 'CanFishInLava falso';
        const { shoot, n } = fire(t);
        killAll(shoot);
        return n >= 1 || `boia ${shoot}: ${n}`;
    });

    check('gancho: no maximo 2', () => {
        const t = itemType('Gancho de Exemplo');
        const hook = sample(t);
        killAll(hook.shoot);
        const p = me();
        for (let i = 0; i < 3; i++) {
            p['void FireGrapple(Item grappleItem)'](hook);
            p.ownedProjectileCounts[hook.shoot] = countOf(hook.shoot);
        }
        const n = countOf(hook.shoot);
        killAll(hook.shoot);
        return n === 2 || n + ' gancho(s)';
    });

    // O prisma: segurado (channel) por alguns quadros, ate o raio nascer.
    const laser = itemType('Prisma de Exemplo');
    laserSlot = me().inventory[9].type;
    me().inventory[9]['void SetDefaults(int Type, ItemVariant variant)'](laser, null);
    holding = true;
    me().channel = true;
    fire(laser, true);
}

let holding = false;
let laserSlot = 0;

function projType(name) {
    for (let t = bl.projectiles.vanillaCount; t < bl.projectiles.vanillaCount + 64; t++) {
        if (!bl.projectiles.isModProjectile(t)) break;
        if (Terraria.Lang['LocalizedText GetProjectileName(int type)'](t).Value === name) return t;
    }
    return -1;
}

function laserCheck() {
    holding = false;
    check('prisma: raio', () => {
        const beam = projType('Raio de Exemplo');
        const n = countOf(beam);
        return n >= 1 || `raio ${beam}: ${n}, prisma: ${countOf(me().inventory[9].shoot)}`;
    });
    me().inventory[9]['void SetDefaults(int Type, ItemVariant variant)'](laserSlot, null);
}

function late() {
    const got = (k) => (count[k] || 0) >= 1 || `${k} = 0`;
    const all = (...r) => r.find((x) => x !== true) || true;
    check('projetil: OnSpawn', () => count.OnSpawn === 1 || 'OnSpawn = ' + count.OnSpawn);
    check('projetil: AIType', () => got('AIType restaurado'));
    check('projetil: OnTileCollide', () => got('OnTileCollide'));
    check('projetil: GetAlpha, PreDraw, PostDraw', () => all(got('GetAlpha'), got('PreDraw'), got('PostDraw')));
    check('projetil: CanDamage, ModifyDamageHitbox', () => all(got('CanDamage'), got('ModifyDamageHitbox')));
    killAll(BOUNCE);
    bl.log('exmod2 FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (holding) {
        self.channel = true;
        self.selectedItemState.selected = 9;
        self.statMana = self.statManaMax2;
    }
    ++frames;
    if (frames === 150) laserCheck();
    if (frames === 90) run();
    if (frames === 210) late();
});
bl.log('exmod2: carregado');
