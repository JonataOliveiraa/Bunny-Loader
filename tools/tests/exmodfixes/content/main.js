// As correções do Example Mod, cada uma pelo caminho do jogo:
//   gancho: Main.projHook do projétil de mod e o QuickGrapple (o botão de
//     gancho do celular) atirando ele, do inventário e do slot de gancho;
//   material: Item.material do item de mod que é ingrediente (o Guia lê isso);
//   bloco de gemas: a luz dele numa caixa fechada, contra a do de diamante;
//   fogo vivo: colocar em cima de outro fogo vivo (o teste de colocação do jogo);
//   relógio: a origem do TileObjectData e um toque de verdade nele;
//   armadura: o conjunto no ArmorSetBonuses (tooltip do 1.4.5) e o efeito vestido;
//   acessórios: escudo com escudo e bota com bota recusados, e a troca indo
//     para o slot do mesmo tipo;
//   chicote: o NPC em cima do chicote desenhado leva o golpe (contra um
//     chicote do jogo, pelo mesmo caminho).
// Loga "fix <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { ItemID, TileID, WallID, NPCID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('fix ' + label + ': ok');
        else { fails++; bl.log('fix ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('fix ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];
const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const place = (x, y, type, style = 0) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, style);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const typeAt = (x, y) => bl.tiles.typeAt(x, y);
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const fillWhip = Terraria.Projectile['void FillWhipControlPoints(Projectile proj, List`1 controlPoints, Player owner, bool getActualCollisionPoints, int frameOffset)'];
const Vector2List = System.Collections.Generic.List.makeGeneric(Vector2.Type);
const brightness = (c) => c.R + c.G + c.B;

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}
let items, projs, tiles;

function newItem(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
}
function setItem(item, type) { item['void SetDefaults(int Type, ItemVariant variant)'](type, null); }
function countProj(type) {
    let n = 0;
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.owner === Main.myPlayer && pr.type === type) n++;
    }
    return n;
}
function killProj(type) {
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.type === type) pr.active = false;
    }
}

let forceUse = false;
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

// O Damage do jogo em cada chicote (quantas vezes e com que ai0): o chicote de
// exemplo colidia sem ferir.
const whipDamageCalls = new Map();
let whipTarget = null;   // o NPC do passo atual do chicote mirado
Terraria.Projectile['void Damage()'].hook((original, self) => {
    if (!Terraria.ID.ProjectileID.Sets.IsAWhip[self.type]) return original(self);

    const before = whipTarget && whipTarget.active ? whipTarget.life : -1;
    original(self);
    let mark = String(Math.round(self.ai.val0));
    if (before >= 0) {
        // Os pontos que o Damage acabou de usar, no mesmo sub-quadro.
        const hit = self['bool Colliding(Rectangle myRect, Rectangle targetRect)'](self['Rectangle Damage_GetHitbox()'](), whipTarget.Hitbox);
        if (hit) mark += whipTarget.life < before ? '*' : '!';
    }
    const list = whipDamageCalls.get(self.whoAmI) || [];
    list.push(mark);
    whipDamageCalls.set(self.whoAmI, list);
    return undefined;
});

// O relógio: o toque de verdade chega aqui (o RightClick do exemplo diz a hora no chat).
let clockSaid = '', clockArea = null, clockReached = false, clockLogs = 0;
Terraria.Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'].hook((original, text, r, g, b, onlyMine) => {
    if (/\d+:\d\d/.test(text)) clockSaid = text;
    original(text, r, g, b, onlyMine);
});
Terraria.Player['void TileInteractionsUse(int myX, int myY)'].hook((original, self, x, y) => {
    if (clockArea && x >= clockArea.x && x < clockArea.x + 2 && y >= clockArea.y && y < clockArea.y + 5) {
        if (!clockReached || (self.tileInteractAttempted && clockLogs++ < 6)) bl.log(`fix relógio: toque no tile ${bl.tiles.typeAt(x, y)} (${x},${y}): tentativa ${self.tileInteractAttempted}, soltou ${self.releaseUseTile}`);
        clockReached = true;
    }
    return original(self, x, y);
});

// ---- a área de testes, à direita do jogador ----
let area = null;
const saved = {};
function buildArea() {
    const p = me();
    const px = Math.floor((p.position.X + p.width / 2) / 16);
    const gy = Math.floor((p.position.Y + p.height) / 16);
    area = { x0: px + 4, x1: px + 34, gy };
    for (let x = area.x0 - 1; x <= area.x1 + 1; x++) {
        for (let y = gy - 14; y < gy; y++) {
            kill(x, y);
            W['void KillWall(int i, int j, bool fail)'](x, y, false);
            tileAt(x, y).liquid = 0;
        }
        if (typeAt(x, gy) < 0) place(x, gy, TileID.Stone);
    }
}
function clearArea() {
    if (!area) return;
    for (let x = area.x0 - 1; x <= area.x1 + 1; x++) {
        for (let y = area.gy - 14; y < area.gy; y++) {
            kill(x, y);
            W['void KillWall(int i, int j, bool fail)'](x, y, false);
        }
    }
}

// Uma caixa 5x5 de pedra com parede por dentro (sem luz do céu), o bloco no meio.
function lightBox(x, type) {
    const y = area.gy - 6;
    for (let i = 0; i < 5; i++) for (let j = 0; j < 5; j++) {
        W['void PlaceWall(int i, int j, int type, bool mute)'](x + i, y + j, WallID.Stone, true);
        if (i === 0 || j === 0 || i === 4 || j === 4) place(x + i, y + j, TileID.Stone);
    }
    if (type > 0) place(x + 2, y + 2, type);
    return { x: x + 1, y: y + 1 };   // a célula de ar medida
}

const steps = [];
function step(frames, fn) { steps.push([frames, fn]); }

step(1, () => {
    check('gancho: projHook', () => Main.projHook[projs.ExampleHookProjectile] === true || 'Main.projHook falso');
    const p = me();
    saved.misc4 = p.miscEquips[4].type;
    saved.slot0 = p.inventory[0].type;
    saved.selected = p.selectedItemState.selected;
    killProj(projs.ExampleHookProjectile);
    setItem(p.miscEquips[4], 0);
    setItem(p.inventory[0], items.ExampleHookItem);
    p['void QuickGrapple()']();
    saved.hookFromInventory = countProj(projs.ExampleHookProjectile);
});
step(10, (f) => {
    if (f !== 9) return;
    check('gancho: botão com o gancho no inventário', () => saved.hookFromInventory > 0 || 'QuickGrapple não atirou');
    const p = me();
    killProj(projs.ExampleHookProjectile);
    setItem(p.inventory[0], saved.slot0);
    setItem(p.miscEquips[4], items.ExampleHookItem);
    p.releaseHook = true;
    p['void QuickGrapple()']();
    check('gancho: botão com o gancho no slot de gancho', () => countProj(projs.ExampleHookProjectile) > 0 || 'QuickGrapple não atirou');
});
step(10, (f) => {
    if (f !== 9) return;
    killProj(projs.ExampleHookProjectile);
    setItem(me().miscEquips[4], saved.misc4);
});

step(1, () => {
    check('material: ExampleItem', () => {
        const t = items.ExampleItem;
        if (!ItemID.Sets.IsAMaterial[t]) return 'IsAMaterial falso';
        return newItem(t).material === true || 'Item.material falso num item novo';
    });
});

step(1, () => {
    buildArea();
    saved.boxEmpty = lightBox(area.x0 + 6, 0);
    saved.boxVanilla = lightBox(area.x0 + 12, TileID.DiamondGemspark);
    saved.boxMod = lightBox(area.x0 + 18, tiles.ExampleGemsparkBlockOn);
});
step(40, (f) => {
    if (f !== 39) return;
    const at = (b) => brightness(Terraria.Lighting['Color GetColor(int x, int y)'](b.x, b.y));
    const empty = at(saved.boxEmpty), vanilla = at(saved.boxVanilla), mod = at(saved.boxMod);
    check('bloco de gemas: luz', () => mod >= vanilla * 0.8 && mod > empty + 60 ||
        `vazia ${empty}, diamante ${vanilla}, de exemplo ${mod}`);
});

step(1, () => {
    const p = me();
    const x = area.x0 + 26, y = area.gy - 1;
    if (!place(x, y, tiles.ExampleLivingFireTile)) place(x, y, tiles.ExampleLivingFireTile);
    place(x + 2, y, TileID.LivingFire);
    p.selectedItemState.selected = 0;
    const test = (item, tx) => {
        setItem(p.inventory[0], item);
        Terraria.Player.tileTargetX = tx;
        Terraria.Player.tileTargetY = y - 1;
        return p['bool PlaceThing_Tiles_BlockPlacementForAssortedThings(bool canPlace)'](false);
    };
    check('fogo vivo: controle (o do jogo sobre o do jogo)', () => test(ItemID.LivingFireBlock, x + 2) === true || 'nem o do jogo');
    check('fogo vivo: sobre outro fogo vivo', () => typeAt(x, y) === tiles.ExampleLivingFireTile
        ? (test(items.ExampleLivingFire, x) === true || 'recusado') : 'o primeiro bloco nao entrou');
    setItem(p.inventory[0], saved.slot0);
    p.selectedItemState.selected = saved.selected;
});

step(1, () => {
    check('relógio: origem como a do jogo', () => {
        const OD = Terraria.ObjectData.TileObjectData;
        const mine = OD['TileObjectData GetTileData(int type, int style, int alternate)'](tiles.ExampleClock, 0, 0);
        const game = OD['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.GrandfatherClocks, 0, 0);
        return (mine.Origin.X === game.Origin.X && mine.Origin.Y === game.Origin.Y && mine.DrawYOffset === game.DrawYOffset) ||
            `origem ${mine.Origin.X},${mine.Origin.Y} (jogo ${game.Origin.X},${game.Origin.Y}), DrawYOffset ${mine.DrawYOffset} (jogo ${game.DrawYOffset})`;
    });
});

// O toque de verdade: primeiro no relógio do jogo (o controle), depois no de
// exemplo, no mesmo lugar, ao alcance do jogador. Cada um loga "TOQUE <nome>".
function clockTap(label, typeOf) {
    const st = { said: '', reached: false };
    step(1, () => {
        const p = me();
        // O mundo de teste é salvo: um móvel que ficou ao lado do jogador (um
        // manequim vestido não quebra) toma o lugar. Vai para o primeiro livre.
        const px = Math.floor((p.position.X + p.width / 2) / 16), y = area.gy - 1;
        const free = (x) => {
            for (let i = 0; i <= 1; i++) for (let j = 0; j < 5; j++) if (tileAt(x + i, y - j)['bool active()']()) return false;
            return true;
        };
        let x = px + 3;
        for (const dx of [3, -4, 6, -7, 9, -10]) {
            x = px + dx;
            for (let j = 5; j >= 0; j--) for (let i = -1; i <= 2; i++) kill(x + i, y - j);
            if (free(x)) break;
        }
        const placed = W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, typeOf(), true, 0, 0, -1, -1);
        const cells = [];
        for (let j = -5; j <= 1; j++) {
            const row = [];
            for (let i = -1; i <= 2; i++) {
                const c = tileAt(x + i, y + j);
                row.push((c['bool active()']() ? typeAt(x + i, y + j) : '.') + (c.liquid ? '~' + c.liquid : ''));
            }
            cells.push(row.join(' '));
        }
        bl.log(`fix relógio (${label}): PlaceObject ${placed} em ${x},${y}; em volta: ${cells.join(' | ')}`);
        const t = tileAt(x, y);
        clockArea = { x: x - Math.floor(t.frameX % 36 / 18), y: y - Math.floor(t.frameY / 18) };
        clockSaid = '';
        clockReached = false;
        clockLogs = 0;
        const m = ModTile.getModTile(tiles.ExampleClock);
        if (m && !m.__wrapped) {
            const own = m.RightClick;
            m.RightClick = function (i, j) { bl.log(`fix relógio: RightClick ${i},${j}`); return own.call(this, i, j); };
            m.__wrapped = true;
        }
        check(`relógio (${label}): colocado`, () => typeAt(x, y) === typeOf() || 'não entrou');
        const sx = (clockArea.x + 1) * 16 - Main.screenPosition.X, sy = (clockArea.y + 2.5) * 16 - Main.screenPosition.Y;
        bl.log(`fix relógio: TOQUE ${label} em interface ${sx.toFixed(0)},${sy.toFixed(0)} (tela ${Main.screenWidth}x${Main.screenHeight})`);
    });
    step(600, (f) => {
        if (f % 20 === 0 && !st.said && clockArea) {
            bl.log(`fix relógio (${label}) q${f}: smart ${Main.SmartInteractShowingGenuine} em ${Main.SmartInteractX},${Main.SmartInteractY}, ` +
                `alvo ${Terraria.Player.tileTargetX},${Terraria.Player.tileTargetY}, usar tile ${me().controlUseTile}, soltou ${me().releaseUseTile}`);
        }
        if (clockSaid && !st.said) { st.said = clockSaid; st.reached = clockReached; steps[current][0] = f + 1; }
        if (f !== steps[current][0] - 1) return;
        check(`relógio (${label}): o toque chega ao tile`, () => clockReached || st.reached || 'TileInteractionsUse nunca foi chamado no relógio');
        check(`relógio (${label}): o toque diz a hora`, () => !!(st.said || clockSaid) || 'sem a hora no chat');
        kill(clockArea.x, clockArea.y + 4);
        clockArea = null;
    });
}
clockTap('do jogo', () => TileID.GrandfatherClocks);
clockTap('de exemplo', () => tiles.ExampleClock);

step(1, () => {
    const p = me();
    saved.armor = [0, 1, 2].map((i) => p.armor[i].type);
    const { ArmorSetBonuses } = Terraria.DataStructures;
    check('armadura: conjunto no ArmorSetBonuses', () => {
        const sets = ArmorSetBonuses.SetsContaining[items.ExampleHelmet];
        if (!sets || sets.length === 0) return 'SetsContaining vazio para o capacete';
        const tip = sets[0]['string GetTooltipForSinglePiece(int itemType)'](items.ExampleHelmet);
        bl.log('fix armadura: tooltip de uma peça: ' + tip.replace(/\n/g, ' | '));
        return tip.length > 0 || 'tooltip vazio';
    });
    setItem(p.armor[0], items.ExampleHelmet);
    setItem(p.armor[1], items.ExampleBreastplate);
    setItem(p.armor[2], items.ExampleLeggings);
});
step(5, (f) => {
    if (f !== 4) return;
    const p = me();
    check('armadura: efeito vestido uma vez (+20% de dano)', () => (p.meleeDamage >= 1.19 && p.meleeDamage < 1.3) || 'meleeDamage ' + p.meleeDamage.toFixed(2));
    for (let i = 0; i < 3; i++) setItem(p.armor[i], saved.armor[i]);
});

step(1, () => {
    const p = me();
    const ItemSlot = Terraria.UI.ItemSlot;
    const canEquipBoth = ItemSlot['bool CanEquipBothAccessories(Item acc1, Item acc2, bool vanity)'];
    const both = (a, b) => canEquipBoth(a, b, false);
    const shield = newItem(items.ExampleShield), boots = newItem(items.ExampleBoots);
    const cobalt = newItem(ItemID.CobaltShield), hermes = newItem(ItemID.HermesBoots);
    check('acessórios: escudo com escudo recusado', () => both(cobalt, shield) === false || 'aceitou');
    check('acessórios: bota com bota recusada', () => both(hermes, boots) === false || 'aceitou');
    check('acessórios: escudo com bota aceito', () => both(hermes, shield) === true || 'recusou');

    saved.acc = [];
    for (let i = 3; i < 10; i++) { saved.acc.push(p.armor[i].type); setItem(p.armor[i], 0); }
    setItem(p.armor[3], ItemID.HermesBoots);
    setItem(p.armor[5], ItemID.CobaltShield);
    const success = new Ref(false), slot = new Ref(-1);
    const back = ItemSlot['Item ArmorSwap(Item item, out bool success, out int targetSlot)'](newItem(items.ExampleShield), success, slot);
    check('acessórios: a troca vai para o slot do escudo', () => (slot.value === 5 && p.armor[5].type === items.ExampleShield) ||
        `slot ${slot.value}, armor[5] ${p.armor[5].type}, devolveu ${back ? back.type : '-'}`);
    for (let i = 3; i < 10; i++) setItem(p.armor[i], saved.acc[i - 3]);
});

// O chicote: o NPC é posto em cima do chicote desenhado (os pontos do
// desenho) e fica parado; conta os golpes. Primeiro o do jogo, de controle.
// Quem cria a poeira durante o golpe: por tipo de poeira (NewDust e NewDustPerfect).
let dustTypes = null;
Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'].hook(
    (original, pos, w, h, type, sx, sy, alpha, color, scale) => {
        if (dustTypes) dustTypes[type] = (dustTypes[type] || 0) + 1;
        return original(pos, w, h, type, sx, sy, alpha, color, scale);
    });
Terraria.Dust['Dust NewDustPerfect(Vector2 Position, int Type, Nullable<Vector2> Velocity, int Alpha, Color newColor, float Scale)'].hook(
    (original, pos, type, vel, alpha, color, scale) => {
        if (dustTypes) dustTypes['p' + type] = (dustTypes['p' + type] || 0) + 1;
        return original(pos, type, vel, alpha, color, scale);
    });

function whipRun(typeOf, label) {
    const state = { label, frames: 0, collide: 0, hits: 0, npc: null, lastLife: 0, proj: -1, flags: '' };
    step(1, () => {
        const p = me();
        saved.slot0 = p.inventory[0].type;
        setItem(p.inventory[0], typeOf());
        p.selectedItemState.selected = 0;
        const c = p.Center;
        const n = newNpc(null, Math.floor(c.X - 200), Math.floor(c.Y), NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
        state.npc = Main.npc[n];
        state.npc.lifeMax = 50000;
        state.npc.life = 50000;
        state.lastLife = 50000;
        forceUse = true;
        dustTypes = {};
    });
    step(90, (f) => {
        const npc = state.npc;
        if (f === 30) forceUse = false;
        if (!npc || !npc.active) return;
        if (npc.life < state.lastLife) state.hits++;
        state.lastLife = npc.life;
        let dust = 0;
        for (let k = 0; k < Main.dust.length; k++) if (Main.dust[k].active) dust++;
        state.dust = Math.max(state.dust || 0, dust);
        const shoot = me().inventory[0].shoot;
        let proj = null;
        for (let i = 0; i < 1000; i++) {
            const pr = Main.projectile[i];
            if (pr.active && pr.owner === Main.myPlayer && pr.type === shoot) { proj = pr; break; }
        }
        npc.velocity = Vector2.new(0, 0);
        if (!proj) { npc.position = Vector2.new(me().Center.X - 200, npc.position.Y); return; }
        state.frames++;
        if (!state.flags) state.flags = `friendly ${proj.friendly}, dano ${proj.damage}, ${proj.width}x${proj.height}, aiStyle ${proj.aiStyle}, localImun ${proj.usesLocalNPCImmunity}`;

        // Os pontos do desenho (o PreDraw passa o dono) e os da colisão (o
        // Damage do jogo passa null): o NPC vai onde o chicote aparece.
        const drawn = Vector2List.new();
        drawn['void .ctor()']();
        fillWhip(proj, drawn, me(), true, 0);
        const collided = Vector2List.new();
        collided['void .ctor()']();
        fillWhip(proj, collided, null, true, 0);
        const pts = drawn.ToArray(), cpts = collided.ToArray();
        if (pts.length < 3) return;
        for (let k = 0; k < Math.min(pts.length, cpts.length); k++) {
            state.gap = Math.max(state.gap || 0, Math.hypot(pts[k].X - cpts[k].X, pts[k].Y - cpts[k].Y));
        }
        if (pts.length !== cpts.length) state.counts = `${pts.length}/${cpts.length}`;
        const mid = pts[Math.floor(pts.length * (f % 2 ? 0.5 : 0.9))];
        npc.position = Vector2.new(mid.X - npc.width / 2, mid.Y - npc.height / 2);
        proj.WhipPointsForCollision.Clear();
        fillWhip(proj, proj.WhipPointsForCollision, null, true, 0);
        const mine = proj['Rectangle Damage_GetHitbox()']();
        if (proj['bool Colliding(Rectangle myRect, Rectangle targetRect)'](mine, npc.Hitbox)) state.collide++;
    });
    step(1, () => {
        check(`chicote (${label}): acerta o NPC em cima dele`, () => state.hits > 0 ||
            `${state.frames} quadros, Colliding ${state.collide}, golpes ${state.hits}; ${state.flags}`);
        const coolWhip = me()['int FindBuffIndex(int type)'](Terraria.ID.BuffID.CoolWhipPlayerBuff) >= 0;
        check(`chicote (${label}): sem o buff do Chicote Frio`, () => !coolWhip || 'o golpe deu o CoolWhipPlayerBuff');
        bl.log(`fix chicote (${label}): pico de ${state.dust} poeira(s) ativas; criadas por tipo: ${JSON.stringify(dustTypes)}`);
        dustTypes = null;
        bl.log(`fix chicote (${label}): ${state.frames} quadros, Colliding verdadeiro em ${state.collide}, golpes ${state.hits}; desenho x colisao: ate ${(state.gap || 0).toFixed(1)} px${state.counts ? ', pontos ' + state.counts : ''}; ${state.flags}`);
        if (state.npc) state.npc.active = false;
        setItem(me().inventory[0], saved.slot0);
    });
}
whipRun(() => ItemID.BlandWhip, 'do jogo');
whipRun(() => items.ExampleWhip, 'de exemplo');

// O caso de verdade: o NPC parado, o golpe mirado para a frente dele. A curva
// (ai[1]) que o item sorteou fica registrada; depois ela é fixada igual nos
// dois chicotes, para a comparação não depender da sorte. `above` põe o NPC
// fora da linha de mira: só a curva o alcança.
const WHIP_CURVE = 0.8;
function whipAimed(typeOf, label, spots) {
    const result = [], curves = [];
    for (const [d, above] of spots) {
        const st = { npc: null, hit: false, aimed: false, collide: 0 };
        step(1, () => {
            const p = me();
            setItem(p.inventory[0], typeOf());
            p.selectedItemState.selected = 0;
            const c = p.Center;
            const n = newNpc(null, Math.floor(c.X + d * 16), Math.floor(c.Y + 12 - above * 16), NPCID.BlueSlime, 0, 0, 0, 0, 0, 255);
            st.npc = Main.npc[n];
            st.npc.lifeMax = 50000;
            st.npc.life = 50000;
            st.npc.noGravity = true;
            whipTarget = st.npc;
            st.spot = Vector2.new(st.npc.position.X, st.npc.position.Y);
            forceUse = true;
            whipDamageCalls.clear();
        });
        step(70, (f) => {
            const npc = st.npc;
            if (f === 2) forceUse = false;
            if (!npc || !npc.active) return;
            npc.position = st.spot;
            npc.velocity = Vector2.new(0, 0);
            if (npc.life < 50000) st.hit = true;
            const shoot = me().inventory[0].shoot;
            for (let i = 0; i < 1000; i++) {
                const pr = Main.projectile[i];
                if (!pr.active || pr.owner !== Main.myPlayer || pr.type !== shoot) continue;
                st.seen = st.seen || new Set();
                st.seen.add(i);
                if (!st.aimed) {
                    curves.push(pr.ai.val1);
                    pr.ai.val1 = WHIP_CURVE;
                    // Mirado na altura do jogador, à frente do NPC: o NPC de
                    // cima fica fora da linha reta.
                    const arm = me().Center;
                    const len = Math.hypot(pr.velocity.X, pr.velocity.Y);
                    pr.velocity = Vector2.new(len, 0);
                    st.aimed = true;
                }
                pr.WhipPointsForCollision.Clear();
                fillWhip(pr, pr.WhipPointsForCollision, null, true, 0);
                if (pr['bool Colliding(Rectangle myRect, Rectangle targetRect)'](pr['Rectangle Damage_GetHitbox()'](), npc.Hitbox)) {
                    st.collide++;
                    if (above) bl.log(`fix chicote mirado (${label}) ${d}+${above}: colide no quadro ${f}, ai0 ${pr.ai.val0.toFixed(0)}, ` +
                        `imune ${npc.immune[Main.myPlayer]}, imune local ${pr.localNPCImmunity['int get_Item(int index)'](npc.whoAmI)}, ` +
                        `linha de visão ${pr['bool CanHitWithMeleeWeapon(Entity ent)'](npc)}, dano ${pr.damage}, friendly ${pr.friendly}`);
                }
            }
        });
        step(1, () => {
            if (above) for (const i of st.seen || []) bl.log(`fix chicote mirado (${label}) ${d}+${above}: Damage() (ai0; * colidiu e feriu, ! colidiu sem ferir) ${(whipDamageCalls.get(i) || []).join(',')}`);
            whipTarget = null;
            whipDamageCalls.clear();
            result.push(`${d}${above ? '+' + above + '↑' : ''}:${st.hit ? 'acertou' : 'errou'}(Colliding ${st.collide}, projéteis ${st.seen ? st.seen.size : 0})`);
            if (st.npc) st.npc.active = false;
        });
    }
    step(1, () => {
        bl.log(`fix chicote mirado (${label}): ${result.join(' ')}; curvas sorteadas ${curves.map((v) => v.toFixed(2)).join(' ')}`);
        setItem(me().inventory[0], saved.slot0);
    });
    return { result, curves };
}
// Sem nada construído no caminho: o chicote não fere através de bloco (ownerHitCheck).
step(1, () => clearArea());
const SPOTS = [[3, 0], [6, 0], [9, 0], [12, 0], [5, 3], [8, 3]];
const aimedGame = whipAimed(() => ItemID.ThornWhip, 'do jogo (espinhos)', SPOTS);
const aimedMod = whipAimed(() => items.ExampleWhip, 'de exemplo', SPOTS);
step(1, () => {
    check('chicote: o item de exemplo sorteia a curva (ai[1])', () =>
        (aimedMod.curves.length > 0 && aimedMod.curves.every((v) => v !== 0)) || 'curvas ' + aimedMod.curves.join(' '));
    // Com a mesma curva, o de exemplo (alcance igual ou maior) acerta onde o do jogo acerta.
    const miss = aimedMod.result.filter((r, i) => r.includes('errou') && aimedGame.result[i].includes('acertou'));
    check('chicote mirado: o de exemplo acerta onde o do jogo acerta', () => miss.length === 0 || miss.join(' '));
});


let frames = 0, current = -1, stepFrame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    const p = me();
    p.statLife = p.statLifeMax2;
    p.fallStart = Math.floor(p.position.Y / 16);
    if (++frames < 90) return;
    if (current < 0) {
        check('preparo', () => {
            items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
            projs = byName(bl.projectiles.vanillaCount, bl.projectiles.isModProjectile, ModProjectile.getModProjectile);
            tiles = byName(bl.tiles.vanillaCount, bl.tiles.isModTile, ModTile.getModTile);
            const need = [items.ExampleHookItem, projs.ExampleHookProjectile, items.ExampleItem, tiles.ExampleGemsparkBlockOn,
                          tiles.ExampleLivingFireTile, items.ExampleLivingFire, tiles.ExampleClock, items.ExampleHelmet,
                          items.ExampleShield, items.ExampleBoots, items.ExampleWhip];
            return need.every((t) => t > 0) || 'Example Mod sem o conteúdo: ' + need.join(',');
        });
        if (fails > 0) { done = true; bl.log('fix FIM: ' + fails + ' falha(s)'); return; }
        current = 0;
        stepFrame = 0;
    }
    try {
        steps[current][1](stepFrame);
    } catch (e) {
        fails++;
        bl.log('fix passo ' + current + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
    if (++stepFrame >= steps[current][0]) {
        stepFrame = 0;
        if (++current >= steps.length) {
            done = true;
            forceUse = false;
            bl.log('fix FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        }
    }
});
bl.log('fix: carregado');

export default class TestExmodFixes extends Mod {}
