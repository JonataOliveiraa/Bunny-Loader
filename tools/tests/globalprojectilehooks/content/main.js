const Main = Terraria.Main, Pr = Terraria.Projectile;
const arrow = Terraria.ID.ProjectileID.WoodenArrowFriendly, hook = Terraria.ID.ProjectileID.Hook;
const types = new Set([arrow, hook]), owned = [], placed = [], events = [];
const options = { grapple: null, remap: null };
const rawType = bl.projectiles.register({
    name: 'RawGlobalProjectileProbe', texture: 'Box.png',
    setDefaults(p) { p.width = p.height = 10; p.aiStyle = -1; p.timeLeft = 600; p.tileCollide = false; }
});
types.add(rawType);
let player, target, ground, drawn, modDrawn, frames = 0, failures = 0, checks = 0, done = false, floor = 0;
const spawnProjectile = Pr['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const spawnNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const source = Terraria.DataStructures.EntitySource_DebugCommand.new();
source['void .ctor()']();
const rect = (x, y, w, h) => Rectangle.new(x, y, w, h);
function log(message) { bl.log('globalprojectilehooks ' + message); }
function check(name, fn) {
    checks++;
    try { if (!fn()) throw Error('resultado falso'); log(name + ': ok'); }
    catch (error) { failures++; log(name + ': FALHOU ' + error); }
}
function state(p) { return p.GetGlobalProjectile(FirstProjectileProbe); }
function record(p, name) {
    const s = state(p);
    if (s.mode) { s.calls[name] = (s.calls[name] || 0) + 1; events.push(name); }
    return s;
}
export class FirstProjectileProbe extends GlobalProjectile {
    InstancePerEntity = true;
    mode = '';
    calls = null;
    SetDefaults(p) { this.calls = {}; }
    AppliesToEntity(p) { return types.has(p.type); }
    PreDraw(p, light) {
        const s = record(p, 'a.PreDraw');
        if (s.mode === 'drawColor') light.value = Color.new(71, 82, 93, 255);
        return s.mode !== 'drawVeto';
    }
    PostDraw(p, light) { const s = record(p, 'a.PostDraw'); s.postR = light.R; }
    GetAlpha(p, light) {
        const s = record(p, 'a.GetAlpha'); s.alphaR = light.R;
        if (s.mode === 'alpha') return Color.new(11, 22, 33, 44);
    }
    Colliding(p, mine, other) {
        const s = record(p, 'a.Colliding');
        if (s.mode === 'collideFalse') return false;
        if (s.mode === 'collideTrue') return true;
    }
    CanDamage(p) { const s = record(p, 'a.CanDamage'); if (s.mode === 'damageVeto') return false; }
    ModifyDamageHitbox(p, box) {
        const s = record(p, 'a.ModifyDamageHitbox');
        if (s.mode === 'hitbox') box.value = rect(21, 32, 43, 54);
    }
    OnTileCollide(p, oldVelocity) {
        const s = record(p, 'a.OnTileCollide'); s.oldY = oldVelocity.Y;
        if (s.mode === 'ground' || s.mode === 'timeout') return false;
        return true;
    }
    TileCollideStyle(p, width, height, fall, anchor) {
        const s = record(p, 'a.TileCollideStyle');
        if (s.mode === 'ground' || s.mode === 'timeout') {
            width.value = height.value = 8; fall.value = false; anchor.value = Vector2.new(.5, .5);
        }
        return s.mode !== 'ghost';
    }
    MinionContactDamage(p) { return record(p, 'a.MinionContactDamage').mode === 'contact'; }
    CanCutTiles(p) {
        const mode = record(p, 'a.CanCutTiles').mode;
        if (mode === 'cutYes') return true;
        if (mode === 'cutNo') return false;
    }
    CutTiles(p) { record(p, 'a.CutTiles'); }
    CanUseGrapple(type, owner) { if (type === hook) { events.push('a.CanUseGrapple'); return options.grapple; } }
    UseGrapple(owner, type) { events.push('a.UseGrapple'); if (options.remap !== null) type.value = options.remap; }
    GrappleCanLatchOnTo(p, owner, tile) {
        const s = record(p, 'a.GrappleCanLatchOnTo');
        if (s.mode === 'latchYes') return true;
        if (s.mode === 'latchNo') return false;
    }
    OnKill(p, time) { const s = record(p, 'a.OnKill'); s.deathTime = time; }
}
export class SecondProjectileProbe extends GlobalProjectile {
    AppliesToEntity(p) { return types.has(p.type); }
    PreDraw(p, light) { record(p, 'b.PreDraw'); return true; }
    PostDraw(p, light) { record(p, 'b.PostDraw'); }
    Colliding(p, mine, other) { record(p, 'b.Colliding'); }
    OnTileCollide(p, oldVelocity) { record(p, 'b.OnTileCollide'); return true; }
    ModifyDamageHitbox(p, box) { if (record(p, 'b.ModifyDamageHitbox').mode === 'hitbox') box.value.Width += 1; }
    CutTiles(p) { record(p, 'b.CutTiles'); }
    CanUseGrapple(type, owner) { if (type === hook) events.push('b.CanUseGrapple'); }
    UseGrapple(owner, type) { events.push('b.UseGrapple'); }
    GrappleCanLatchOnTo(p, owner, tile) { record(p, 'b.GrappleCanLatchOnTo'); }
}
export class GlobalDrawProjectile extends ModProjectile {
    Texture = 'Box';
    SetDefaults(p) { p.width = p.height = 10; p.aiStyle = -1; p.timeLeft = 600; p.tileCollide = false; }
    PreDraw(p, light) { record(p, 'm.PreDraw'); return true; }
    PostDraw(p, light) { record(p, 'm.PostDraw'); }
    ModifyDamageHitbox(p, box) { if (state(p).mode === 'hitbox') box.Width = 19; }
}
function spawn(type = arrow, mode = '') {
    const index = spawnProjectile(source, player.Center.X, player.position.Y - 100, 0, 0, type, 20, 0, Main.myPlayer, 0, 0, 0, null);
    if (index < 0 || index >= 1000) throw Error('sem slot');
    const p = Main.projectile[index]; owned.push(p);
    p.aiStyle = -1; p.tileCollide = false; p.penetrate = -1; p.ignoreWater = true; p.noDropItem = true;
    const s = state(p); s.mode = mode; s.calls = {};
    return p;
}
function hit(mode, contact = false) {
    const p = spawn(arrow, mode), pet = Main.projPet[arrow];
    p.friendly = true; p.damage = 40; p.penetrate = -1; p.Center = target.Center;
    if (mode === 'collideTrue') p.position.X += 150;
    target.immune[Main.myPlayer] = 0;
    const before = target.life;
    if (contact) Main.projPet[arrow] = true;
    try { p['void Damage()'](); }
    finally { check('tabela pet restaurada ' + mode, () => !contact || Main.projPet[arrow] === true); Main.projPet[arrow] = pet; p.active = false; }
    return before - target.life;
}
function prepare() {
    const modType = ModProjectile.getTypeByName('GlobalDrawProjectile'); types.add(modType);
    const index = spawnNPC(source, Math.floor(player.Center.X) + 160, Math.floor(player.position.Y), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
    if (index < 0 || index >= 200) throw Error('sem slot NPC');
    target = Main.npc[index]; target.lifeMax = target.life = 100000; target.damage = 0;
    const p = spawn(arrow, 'alpha'), light = Color.new(1, 2, 3, 255);
    check('GetAlpha substitui cor', () => { const c = p['Color GetAlpha(Color newColor)'](light); return c.R === 11 && c.G === 22 && c.B === 33 && c.A === 44; });
    state(p).mode = 'collideFalse';
    check('Colliding false prevalece', () => !p['bool Colliding(Rectangle myRect, Rectangle targetRect)'](rect(0, 0, 10, 10), rect(0, 0, 10, 10)));
    state(p).mode = 'collideTrue';
    check('Colliding true fora da caixa', () => p['bool Colliding(Rectangle myRect, Rectangle targetRect)'](rect(0, 0, 10, 10), rect(100, 100, 10, 10)));
    check('CanDamage false bloqueia NPC', () => hit('damageVeto') === 0);
    check('Colliding false bloqueia NPC sobreposto', () => hit('collideFalse') === 0);
    const wasComplex = Terraria.ID.ProjectileID.Sets.IsAComplexCollision[arrow];
    check('Colliding true causa dano sem sobreposicao', () => hit('collideTrue') > 0);
    check('marca de colisao complexa restaurada', () => Terraria.ID.ProjectileID.Sets.IsAComplexCollision[arrow] === wasComplex);
    check('MinionContactDamage true permite contato', () => hit('contact', true) > 0);
    check('MinionContactDamage false bloqueia contato', () => hit('noContact', true) === 0);
    state(p).mode = 'hitbox';
    check('ModifyDamageHitbox Ref substitui e acumula', () => { const r = p['Rectangle Damage_GetHitbox()'](); return r.X === 21 && r.Y === 32 && r.Width === 44 && r.Height === 54; });
    state(p).mode = 'cutNo'; check('CanCutTiles false', () => !p['bool CanCutTiles()']());
    events.length = 0; p['void CutTiles()']();
    check('CanCutTiles false impede callbacks de corte', () => !events.includes('a.CutTiles') && !events.includes('b.CutTiles'));
    state(p).mode = 'cutYes'; check('CanCutTiles true', () => p['bool CanCutTiles()']());
    events.length = 0; p['void CutTiles()']();
    check('CutTiles globals em ordem', () => events.filter(e => e.includes('CutTiles')).join(',') === 'a.CanCutTiles,a.CutTiles,b.CutTiles');
    const other = spawn(arrow);
    check('InstancePerEntity isola estado', () => state(other) !== state(p) && state(other).mode === '' && state(p).mode === 'cutYes');
    const raw = spawn(rawType, 'damageVeto');
    check('tipo registrado pela API nativa sem ModProjectile', () => !raw.ModProjectile && bl.projectiles.isModProjectile(raw.type));
    raw['void Damage()']();
    check('CanDamage global atende tipo da API nativa', () => state(raw).calls['a.CanDamage'] === 1); raw.active = false;
    p['void SetDefaults(int Type)'](arrow);
    check('SetDefaults recria estado', () => state(p).mode === ''); p.active = false; other.active = false;
    const grapple = Terraria.Item.new(); grapple['void .ctor()'](); grapple['void SetDefaults(int Type, ItemVariant variant)'](Terraria.ID.ItemID.GrapplingHook, null);
    const before = new Set();
    for (let i = 0; i < 1000; i++) if (Main.projectile[i].active) before.add(bl.addressOf(Main.projectile[i]));
    options.grapple = false; events.length = 0; player['void FireGrapple(Item grappleItem)'](grapple);
    check('CanUseGrapple false executa globais', () => events.join(',') === 'a.CanUseGrapple,b.CanUseGrapple');
    let created = [];
    for (let i = 0; i < 1000; i++) { const pr = Main.projectile[i]; if (pr.active && !before.has(bl.addressOf(pr))) created.push(pr); }
    check('CanUseGrapple false impede spawn', () => created.length === 0);
    options.grapple = true; options.remap = arrow; events.length = 0; player['void FireGrapple(Item grappleItem)'](grapple);
    for (let i = 0; i < 1000; i++) { const pr = Main.projectile[i]; if (pr.active && !before.has(bl.addressOf(pr))) { created.push(pr); owned.push(pr); } }
    check('UseGrapple Ref muda projetil e restaura item', () => created.some(pr => pr.type === arrow) && grapple.shoot === hook);
    check('UseGrapple globais em ordem', () => events.includes('a.UseGrapple') && events.indexOf('a.UseGrapple') < events.indexOf('b.UseGrapple'));
    options.remap = rawType; player['void FireGrapple(Item grappleItem)'](grapple);
    let rawGrapple = false;
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && !before.has(bl.addressOf(pr)) && pr.type === rawType) { rawGrapple = true; created.push(pr); owned.push(pr); }
    }
    check('UseGrapple aceita tipo nativo registrado e restaura item', () => rawGrapple && grapple.shoot === hook);
    options.grapple = options.remap = null;
    for (const pr of created) pr.active = false;
    const latch = spawn(hook, 'latchNo');
    const tile = Terraria.Tile.new(); tile['void .ctor()']();
    check('GrappleCanLatchOnTo false', () => !latch['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'](tile));
    state(latch).mode = 'latchYes';
    check('GrappleCanLatchOnTo true em tile vazio', () => latch['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'](tile)); latch.active = false;
    drawn = spawn(arrow, 'drawVeto'); modDrawn = spawn(modType, 'drawVeto');
    const x = Math.floor(player.Center.X / 16), top = Math.floor(player.position.Y / 16);
    const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];
    let y = top - 8;
    while (y > top - 35 && solid(Vector2.new((x - 3) * 16, (y - 4) * 16), 7 * 16, 6 * 16)) y--;
    if (y <= top - 35) throw Error('sem ar livre para plataforma'); floor = y * 16;
    for (let col = x - 3; col <= x + 3; col++) {
        const tile = Main.tile['Tile get_Item(int x, int y)'](col, y);
        if (tile && tile['bool active()']()) throw Error('plataforma pisaria em tile existente');
        if (!Terraria.WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](col, y, Terraria.ID.TileID.Platforms, true, true, -1, 0)) throw Error('plataforma nao criada');
        placed.push([col, y]);
    }
    ground = spawn(arrow, 'ground'); ground.tileCollide = true; ground.width = ground.height = 20;
    ground.Center = Vector2.new(x * 16, floor - 20); ground.velocity = Vector2.new(0, 4);
    const ghost = spawn(arrow, 'ghost'); ghost.tileCollide = true; ghost.Center = Vector2.new((x + 2) * 16, floor - 20); ghost.velocity = Vector2.new(0, 4);
    ghost.timeLeft = 180; state(ground).ghost = ghost;
}
function cleanup() {
    done = true; options.grapple = options.remap = null;
    for (const p of owned) p.active = false;
    if (target) target.active = false;
    for (const [x, y] of placed) Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
}
function finish() {
    const s = state(ground);
    check('TileCollideStyle e OnTileCollide no piso', () => s.calls['a.TileCollideStyle'] > 0 && s.calls['a.OnTileCollide'] > 0 && s.oldY > 0);
    check('OnTileCollide false conserva vanilla no piso', () => ground.active && ground.Center.Y <= floor && ground.velocity.Y === 0);
    check('TileCollideStyle false atravessa e restaura flag', () => s.ghost.Center.Y > floor + 20 && s.ghost.tileCollide);
    check('PreDraw false e PostDraw continuam em vanilla', () => state(drawn).calls['a.PreDraw'] > 0 && state(drawn).calls['b.PreDraw'] > 0 && state(drawn).calls['a.PostDraw'] > 0 && state(drawn).calls['b.PostDraw'] > 0);
    check('PreDraw global veto pula pre local mas chama post local', () => !state(modDrawn).calls['m.PreDraw'] && state(modDrawn).calls['m.PostDraw'] > 0);
    state(drawn).mode = state(modDrawn).mode = 'drawColor';
    s.mode = 'timeout'; ground.timeLeft = 1; ground.velocity.Y = .5;
}
function report() {
    check('cor Ref chega ao GetAlpha e PostDraw nativos', () => state(drawn).alphaR === 71 && state(drawn).postR === 71);
    check('OnKill chamado uma vez por tempo no piso', () => !ground.active && state(ground).calls['a.OnKill'] === 1 && state(ground).deathTime === 0);
    const sample = state(drawn), count = sample.calls['a.Colliding'] || 0;
    const box = rect(0, 0, 10, 10), collide = drawn['bool Colliding(Rectangle myRect, Rectangle targetRect)'];
    const times = [];
    for (let r = 0; r < 7; r++) {
        const start = performance.now();
        for (let i = 0; i < 10000; i++) collide(box, box);
        times.push((performance.now() - start) * 1000 / 10000);
    }
    times.sort((a, b) => a - b);
    check('estado preservado e 70000 despachos sem perda', () => state(drawn) === sample && sample.calls['a.Colliding'] === count + 70000);
    log('benchmark Colliding_2 mediana_us=' + times[3].toFixed(3));
    cleanup(); log('FIM checks=' + checks + ' falhas=' + failures);
}
Pr['void AI()'].hook((original, p) => {
    original(p);
    if (!done && ground && bl.addressOf(p) === bl.addressOf(ground) && p.active) p.velocity.Y = Math.min(p.velocity.Y + .5, 6);
});
Terraria.Player['void Update(int i)'].hook((original, self, index) => {
    original(self, index);
    if (done || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;
    try {
        if (frames === 60) { player = self; prepare(); }
        if (frames === 110) finish();
        if (frames === 125) report();
    } catch (error) { failures++; log('execucao: FALHOU ' + error + ' ' + error.stack); cleanup(); log('FIM checks=' + checks + ' falhas=' + failures); }
});
export default class GlobalProjectileHookTests extends Mod {}
