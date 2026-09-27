// Armaduras e equipáveis do Example Mod: o EquipLoader (slots depois dos do
// jogo, Count e tabelas crescidos), o conjunto (IsArmorSet/UpdateArmorSet e o
// setBonus), as asas (WingStats e VerticalWingSpeeds), a barba e os
// acessórios com textura vestida. Veste no jogador, confere e tira no fim.
// Precisa do Example Mod ligado. Loga "armor <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ArmorIDs, BuffID } = Terraria.ID;
const TextureAssets = Terraria.GameContent.TextureAssets;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('armor ' + label + ': ok');
        else { fails++; bl.log('armor ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('armor ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const me = () => Main.player[Main.myPlayer];
const T = (name) => ModContent.ItemType(name);
const sample = (type) => Terraria.ID.ContentSamples.ItemsByType.get_Item(type);
const setArmor = (slot, type) => me().armor[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
const near = (a, b) => Math.abs(a - b) < 1e-3;

// Asas deste teste: HorizontalWingSpeeds e WingUpdate (TestWings_Wings.png).
const wingCalls = { update: 0 };
class TestWings extends ModItem {
    SetStaticDefaults() { this.SetWingStats(60, 7, 1.5); }

    SetDefaults() {
        this.Item.width = 22;
        this.Item.height = 20;
        this.Item.accessory = true;
    }

    HorizontalWingSpeeds(item, player, speed, acceleration) {
        speed.value = 12.5;
        acceleration.value *= 2;
    }

    WingUpdate(player, inUse) {
        wingCalls.update++;
        return true;
    }
}
const TEST_WINGS = ModItem.register(TestWings);

// Os ganchos de conjunto e de asa do GlobalItem, contados.
const counts = { armorSet: 0, preVanity: 0, vanity: 0, shadows: 0, setMatch: 0, vertical: 0, horizontal: 0, wingUpdate: 0 };
const costumeHead = () => EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Head);
class TestArmorGlobal extends GlobalItem {
    IsArmorSet(head, body, legs) { return head.type === T('ExampleHelmet') ? 'teste' : ''; }
    UpdateArmorSet(player, set) { if (set === 'teste' && player.whoAmI === Main.myPlayer) counts.armorSet++; }
    IsVanitySet(head, body, legs) { return head === costumeHead() ? 'blocky' : ''; }
    PreUpdateVanitySet(player, set) { if (set === 'blocky') counts.preVanity++; }
    UpdateVanitySet(player, set) { if (set === 'blocky') counts.vanity++; }
    ArmorSetShadows(player, set) { if (set === 'blocky') counts.shadows++; }
    SetMatch(armorSlot, type, male, equipSlot, robes) { counts.setMatch++; }
    VerticalWingSpeeds(item, player, a, b, c, d, e) { counts.vertical++; }
    HorizontalWingSpeeds(item, player, speed, acceleration) { counts.horizontal++; }
    WingUpdate(wings, player, inUse) { counts.wingUpdate++; return false; }
}
GlobalItem.register(TestArmorGlobal);

const saved = [];
let base = null;
function snapshot(p) {
    return {
        defense: p.statDefense, melee: p.meleeDamage, magic: p.magicDamage, minion: p.minionDamage,
        mana: p.statManaMax2, minions: p.maxMinions, speed: p.moveSpeed,
    };
}

function staticChecks() {
    const helmet = T('ExampleHelmet'), body = T('ExampleBreastplate'), legs = T('ExampleLeggings');
    const headSlot = EquipLoader.GetEquipSlot('ExampleHelmet', EquipType.Head);
    const bodySlot = EquipLoader.GetEquipSlot('ExampleBreastplate', EquipType.Body);
    const legSlot = EquipLoader.GetEquipSlot('ExampleLeggings', EquipType.Legs);
    const wingSlot = EquipLoader.GetEquipSlot('ExampleWings', EquipType.Wings);
    bl.log(`armor: slots cabeça ${headSlot}, corpo ${bodySlot}, pernas ${legSlot}, asas ${wingSlot}; ` +
           `Count ${ArmorIDs.Head.Count}/${ArmorIDs.Body.Count}/${ArmorIDs.Legs.Count}/${ArmorIDs.Wing.Count}`);

    check('slots depois dos do jogo', () => {
        const v = EquipLoader.VanillaCount(EquipType.Head);
        return (v > 200 && headSlot >= v && headSlot < ArmorIDs.Head.Count &&
                bodySlot >= EquipLoader.VanillaCount(EquipType.Body) && bodySlot < ArmorIDs.Body.Count &&
                legSlot >= EquipLoader.VanillaCount(EquipType.Legs) && legSlot < ArmorIDs.Legs.Count) ||
            `vanilla ${v}, slots ${headSlot}/${bodySlot}/${legSlot}`;
    });

    check('amostras com o slot (SetDefaults)', () => {
        const h = sample(helmet), b = sample(body), l = sample(legs);
        return (h.headSlot === headSlot && b.bodySlot === bodySlot && l.legSlot === legSlot && h.bodySlot === -1) ||
            `${h.headSlot}/${b.bodySlot}/${l.legSlot}, capacete.bodySlot ${h.bodySlot}`;
    });

    check('texturas no TextureAssets', () => {
        const head = TextureAssets.ArmorHead[headSlot], comp = TextureAssets.ArmorBodyComposite[bodySlot];
        const leg = TextureAssets.ArmorLeg[legSlot], wing = TextureAssets.Wings[wingSlot];
        if (!head || !comp || !leg || !wing) return `cabeça ${!!head}, corpo ${!!comp}, pernas ${!!leg}, asas ${!!wing}`;
        const tex = head.Value;
        return (tex && tex.Width > 0 && TextureAssets.ArmorBody[bodySlot] === comp && TextureAssets.ArmorArm[bodySlot] === comp) ||
            'textura vazia ou corpo sem as outras folhas';
    });

    check('tabelas crescidas (Sets, Item.*Type)', () => {
        const n = ArmorIDs.Head.Count;
        const f2b = ArmorIDs.Head.Sets.FrontToBackID;
        return (f2b.length === n && f2b[headSlot] === -1 && ArmorIDs.Body.Sets.UsesNewFramingCode[bodySlot] === true &&
                Terraria.Item.headType[headSlot] === helmet && Terraria.Item.bodyType[bodySlot] === body &&
                Terraria.Item.legType[legSlot] === legs && TextureAssets.ArmorHead.length === n) ||
            `FrontToBackID ${f2b.length}/${n} = ${f2b[headSlot]}, framing ${ArmorIDs.Body.Sets.UsesNewFramingCode[bodySlot]}, ` +
            `headType ${Terraria.Item.headType[headSlot]}`;
    });

    check('asas: WingStats do SetStaticDefaults (this.Item.wingSlot)', () => {
        const s = ArmorIDs.Wing.Sets.Stats[wingSlot];
        return (s.FlyTime === 180 && near(s.AccRunSpeedOverride, 9) && near(s.AccRunAccelerationMult, 2.5)) ||
            `FlyTime ${s.FlyTime}, speed ${s.AccRunSpeedOverride}, accel ${s.AccRunAccelerationMult}`;
    });

    check('barba: UseHairColor do SetStaticDefaults', () => {
        const slot = EquipLoader.GetEquipSlot('ExampleBeard', EquipType.Beard);
        return (slot >= 0 && ArmorIDs.Beard.Sets.UseHairColor[slot] === true) || `slot ${slot}`;
    });

    check('acessórios do Example Mod ganharam a textura vestida', () => {
        const shoe = sample(T('ExampleBoots')).shoeSlot, shield = sample(T('ExampleShield')).shieldSlot;
        const back = sample(T('WaspNest')).backSlot;
        return (shoe >= EquipLoader.VanillaCount(EquipType.Shoes) && shield >= EquipLoader.VanillaCount(EquipType.Shield) &&
                back >= EquipLoader.VanillaCount(EquipType.Back) && !!TextureAssets.AccShoes[shoe]) ||
            `botas ${shoe}, escudo ${shield}, ninho ${back}`;
    });
}

function equipAll() {
    const p = me();
    for (let s = 0; s < 20; s++) saved.push(p.armor[s].type);
    base = snapshot(p);
    setArmor(0, T('ExampleHelmet'));
    setArmor(1, T('ExampleBreastplate'));
    setArmor(2, T('ExampleLeggings'));
    setArmor(3, T('ExampleWings'));
    setArmor(4, T('ExampleBeard'));
    setArmor(5, T('ExampleBoots'));
    setArmor(6, T('WaspNest'));
    bl.log('armor: conjunto, asas, barba, botas e ninho vestidos');
}

function equippedChecks() {
    const p = me(), now = snapshot(p);
    check('o jogador veste os slots de mod', () =>
        (p.head === sample(T('ExampleHelmet')).headSlot && p.body === sample(T('ExampleBreastplate')).bodySlot &&
         p.legs === sample(T('ExampleLeggings')).legSlot) || `head ${p.head}, body ${p.body}, legs ${p.legs}`);
    check('defesa das três peças', () => now.defense - base.defense >= 16 || `de ${base.defense} para ${now.defense}`);
    check('UpdateEquip: mana, lacaio, velocidade, imunidade', () =>
        (now.mana - base.mana === 20 && now.minions - base.minions === 1 && now.speed > base.speed &&
         p.buffImmune[BuffID.OnFire] === true) ||
        `mana +${now.mana - base.mana}, lacaios +${now.minions - base.minions}, velocidade ${base.speed} -> ${now.speed}`);
    check('conjunto: +20% de dano e o setBonus', () =>
        (now.melee - base.melee > 0.19 && now.magic - base.magic > 0.19 && now.minion - base.minion > 0.19 &&
         /20/.test(p.setBonus) && !/ArmorSetBonus/.test(p.setBonus)) ||
        `melee ${base.melee} -> ${now.melee}, setBonus "${p.setBonus}"`);
    check('asas: wings e wingTimeMax', () => {
        const slot = sample(T('ExampleWings')).wingSlot;
        return (p.wings === slot && p.wingTimeMax === 180) || `wings ${p.wings} (slot ${slot}), wingTimeMax ${p.wingTimeMax}`;
    });
    check('barba, botas e ninho no corpo', () =>
        (p.beard === sample(T('ExampleBeard')).beardSlot && p.shoe === sample(T('ExampleBoots')).shoeSlot &&
         p.back === sample(T('WaspNest')).backSlot) || `beard ${p.beard}, shoe ${p.shoe}, back ${p.back}`);

    // O caminho comum do WingMovement com os valores do VerticalWingSpeeds:
    // parado (Y = 0), sobe 0,135 do constantAscend e mais 0,15 do rising.
    check('asas: VerticalWingSpeeds no WingMovement', () => {
        const slot = sample(T('ExampleWings')).wingSlot;
        const saveY = p.velocity.Y, saveTime = p.wingTime, saveGrav = p.gravDir, saveLogic = p.wingsLogic;
        p.wingsLogic = slot;
        p.gravDir = 1;
        p.velocity.Y = 0;
        p.wingTime = 50;
        p['void WingMovement()']();
        const y = p.velocity.Y, time = p.wingTime;
        p.velocity.Y = saveY;
        p.wingTime = saveTime;
        p.gravDir = saveGrav;
        p.wingsLogic = saveLogic;
        return (near(y, -0.285) && near(time, 49)) || `velocity.Y ${y}, wingTime ${time}`;
    });
}

function partialSet() {
    setArmor(2, 0);
    bl.log('armor: sem as calças');
}

function partialChecks() {
    const p = me(), now = snapshot(p);
    check('conjunto incompleto: sem bônus nem setBonus', () =>
        (p.setBonus === '' && now.melee - base.melee < 0.01) || `setBonus "${p.setBonus}", melee ${base.melee} -> ${now.melee}`);
}

function reequip() {
    setArmor(2, T('ExampleLeggings'));
}

// ---- fantasia (AddEquipTexture, EquipTexture própria, FrameEffects, vaidade) ----
function costumeStatic() {
    const head = costumeHead(), alt = EquipLoader.GetEquipSlot('BlockyAlt', EquipType.Head);
    const body = EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Body);
    check('fantasia: AddEquipTexture com nome e EquipTexture própria', () => {
        const tex = EquipLoader.GetEquipTexture(EquipType.Head, head);
        return (head > 0 && alt > 0 && alt !== head && tex && tex.constructor.name === 'BlockyHead' &&
                tex.Item === ModContent.GetModItem(T('ExampleCostume'))) ||
            `cabeça ${head}, alternativa ${alt}, textura ${tex && tex.constructor.name}`;
    });
    check('fantasia: AutoloadEquip = [] e o item veste a primeira textura', () =>
        (sample(T('ExampleCostume')).headSlot === head && sample(T('ExampleCostume')).bodySlot === body) ||
        `headSlot ${sample(T('ExampleCostume')).headSlot}, bodySlot ${sample(T('ExampleCostume')).bodySlot}`);
    check('DrawHead (HidesHead do jogo) e HidesTopSkin do tModLoader', () =>
        (ArmorIDs.Head.Sets.DrawHead[head] === false && ArmorIDs.Head.Sets.HidesHead[head] === true &&
         ArmorIDs.Body.Sets.HidesTopSkin[body] === true && ArmorIDs.Body.Sets.HidesTopSkin[1] === false) ||
        `DrawHead ${ArmorIDs.Head.Sets.DrawHead[head]}, HidesHead ${ArmorIDs.Head.Sets.HidesHead[head]}`);
}

// No acessório de vaidade (o FrameEffects troca tudo) e na cabeça de vaidade:
// o PreUpdateVanitySet vem ANTES do FrameEffects, como no tModLoader, e só vê
// a cabeça que a armadura desenha.
function wearCostume() {
    setArmor(13, T('ExampleCostume'));
    setArmor(10, T('ExampleCostume'));
    bl.log('armor: fantasia no slot de vaidade');
}

function costumeChecks() {
    const p = me();
    check('FrameEffects: o fantasia troca o que se desenha', () =>
        (p.head === costumeHead() && p.body === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Body) &&
         p.legs === EquipLoader.GetEquipSlot('ExampleCostume', EquipType.Legs)) ||
        `head ${p.head} (fantasia ${costumeHead()}), body ${p.body}, legs ${p.legs}`);
    check('vaidade: PreUpdateVanitySet, UpdateVanitySet e sombras pelo slot desenhado', () =>
        (counts.preVanity > 0 && counts.vanity > 0 && counts.shadows > 0) ||
        `pre ${counts.preVanity}, update ${counts.vanity}, sombras ${counts.shadows}`);
    check('Global: conjunto de armadura pelo nome', () => counts.armorSet > 0 || 'UpdateArmorSet ' + counts.armorSet);
}

// ---- manto (SetMatch) ----
function wearRobe() {
    setArmor(13, 0);
    setArmor(10, 0);
    setArmor(11, T('ExampleRobe'));   // corpo de vaidade
    bl.log('armor: manto no corpo de vaidade');
}

function robeChecks() {
    const p = me();
    const robeBody = sample(T('ExampleRobe')).bodySlot;
    const robeLegs = EquipLoader.GetEquipSlot('ExampleRobe', EquipType.Legs);
    check('SetMatch: o manto desenha as pernas dele (wearsRobe)', () =>
        (p.body === robeBody && p.legs === robeLegs && p.wearsRobe === true && counts.setMatch > 0) ||
        `body ${p.body} (${robeBody}), legs ${p.legs} (${robeLegs}), wearsRobe ${p.wearsRobe}, Global ${counts.setMatch}`);
    check('HidesHands = false do manto (ModHelpers)', () =>
        ArmorIDs.Body.Sets.HidesHands[robeBody] === false || String(ArmorIDs.Body.Sets.HidesHands[robeBody]));
    setArmor(11, 0);
}

// ---- asas: HorizontalWingSpeeds, WingUpdate e os Globais ----
function wingChecks() {
    const p = me();
    setArmor(3, TEST_WINGS);
    const slot = sample(TEST_WINGS).wingSlot;
    const save = { logic: p.wingsLogic, wings: p.wings, speed: p.accRunSpeed, accel: p.runAcceleration, frame: p.wingFrame };

    check('asas: SetWingStats', () => ArmorIDs.Wing.Sets.Stats[slot].FlyTime === 60 || 'FlyTime ' + ArmorIDs.Wing.Sets.Stats[slot].FlyTime);

    p.wingsLogic = slot;
    p.wings = slot;
    p.accRunSpeed = 3;
    p.runAcceleration = 0.1;
    p['void WingAirLogicTweaks()']();
    const speed = p.accRunSpeed, accel = p.runAcceleration;
    check('asas: HorizontalWingSpeeds depois do WingAirLogicTweaks', () =>
        (near(speed, 12.5) && near(accel, 0.1 * 1.5 * 2) && counts.horizontal > 0) ||
        `accRunSpeed ${speed}, runAcceleration ${accel}, Global ${counts.horizontal}`);

    p.wingFrame = 2;
    p['void WingFrame(bool wingFlap)'](true);
    check('asas: WingUpdate = true pula o WingFrame do jogo', () =>
        (wingCalls.update > 0 && p.wingFrame === 2 && counts.wingUpdate > 0) ||
        `WingUpdate ${wingCalls.update}, wingFrame ${p.wingFrame}, Global ${counts.wingUpdate}`);
    check('asas: VerticalWingSpeeds do Global', () => counts.vertical > 0 || 'vertical ' + counts.vertical);

    Object.assign(p, { wingsLogic: save.logic, wings: save.wings, accRunSpeed: save.speed, runAcceleration: save.accel, wingFrame: save.frame });
    setArmor(3, T('ExampleWings'));
}

// ---- manequim (TEDisplayDoll) ----
const TEDisplayDoll = Terraria.GameContent.Tile_Entities.TEDisplayDoll;
const W = Terraria.WorldGen;
let doll = null;
function placeDoll() {
    const p = me();
    const x = Math.floor(p.Center.X / 16) + 4, feet = Math.floor((p.position.Y + p.height) / 16);
    const kill = W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
    const place = W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'];
    const solid = (i, j) => { const t = bl.tiles.typeAt(i, j); return t >= 0 && Main.tileSolid[t]; };

    // O chão das duas colunas: o primeiro sólido de cima para baixo (grama baixa não conta).
    let ground = feet - 2;
    while (ground < feet + 8 && !solid(x, ground) && !solid(x + 1, ground)) ground++;
    const placed = [];
    for (let dx = 0; dx < 2; dx++) {
        for (let dy = 1; dy <= 3; dy++) kill(x + dx, ground - dy, false, false, true);
        if (!solid(x + dx, ground)) {
            if (bl.tiles.typeAt(x + dx, ground) >= 0) kill(x + dx, ground, false, false, true);
            if (place(x + dx, ground, Terraria.ID.TileID.Stone, true, true, -1, 0)) placed.push(x + dx);
        }
    }
    const cells = [];
    for (let dy = 3; dy >= 0; dy--) cells.push([0, 1].map((dx) => bl.tiles.typeAt(x + dx, ground - dy)).join('/'));
    bl.log('armor: manequim em ' + x + ',' + (ground - 1) + ', celulas (de cima ao chao) ' + cells.join(' | ') +
           ', jogador ' + p.position.X.toFixed(0) + ',' + p.position.Y.toFixed(0) + ' no ar ' + (p.velocity.Y !== 0));
    const ok = W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](
        x, ground - 1, 470, true, 0, 0, -1, -1);
    const id = ok ? TEDisplayDoll['int Hook_AfterPlacement(int x, int y, int type, int style, int direction, int alternate)'](x, ground - 1, 470, 0, -1, 0) : -1;
    const te = id >= 0 ? Terraria.DataStructures.TileEntity.ByID.get_Item(id) : null;
    doll = { x, ground, placed, te };
    check('manequim posto', () => !!te || `PlaceObject ${ok}, id ${id}`);
    if (!te) return;

    const equip = te._equip;
    equip[0]['void SetDefaults(int Type, ItemVariant variant)'](T('ExampleHelmet'), null);
    equip[1]['void SetDefaults(int Type, ItemVariant variant)'](T('ExampleBreastplate'), null);
    equip[2]['void SetDefaults(int Type, ItemVariant variant)'](T('ExampleLeggings'), null);
    bl.log('armor: manequim vestido em ' + x + ',' + (ground - 1));
}

function dollChecks() {
    const dp = TEDisplayDoll._dollPlayer;
    check('manequim desenha os slots de mod', () =>
        (dp && dp.head === sample(T('ExampleHelmet')).headSlot && dp.body === sample(T('ExampleBreastplate')).bodySlot) ||
        (dp ? `head ${dp.head}, body ${dp.body}` : 'sem _dollPlayer (o manequim nao foi desenhado)'));
}

function finalLook() {
    setArmor(13, T('ExampleCostume'));
    bl.log('armor: fantasia e manequim para a captura');
}

function removeDoll() {
    if (!doll) return;
    const kill = W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
    kill(doll.x, doll.ground - 1, false, false, true);
    for (const x of doll.placed) kill(x, doll.ground, false, false, true);
}

// Para a captura de tela: meio-dia e sem inimigos em volta.
function stage() {
    Main.dayTime = true;
    Main.time = 27000;
}
function clearHostiles() {
    for (let n = 0; n < 200; n++) {
        const npc = Main.npc[n];
        if (npc.active && !npc.friendly && !npc.townNPC) npc.active = false;
    }
}

function restore() {
    removeDoll();
    for (let s = 0; s < saved.length; s++) setArmor(s, saved[s]);
    bl.log('armor FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    // Vivo o teste todo: morto, o jogador vira fantasma e a armadura nem é desenhada.
    if (!self.dead) self.statLife = self.statLifeMax2;
    ++frames;
    if (frames % 30 === 0) clearHostiles();
    if (frames === 60) stage();
    if (frames === 90) { staticChecks(); equipAll(); }
    if (frames === 120) equippedChecks();
    if (frames === 130) partialSet();
    if (frames === 150) partialChecks();
    if (frames === 160) reequip();
    if (frames === 170) { costumeStatic(); wearCostume(); }
    if (frames === 200) costumeChecks();
    if (frames === 210) wearRobe();
    if (frames === 300) robeChecks();
    if (frames === 310) wingChecks();
    if (frames === 320) placeDoll();
    if (frames === 360) { dollChecks(); finalLook(); }
    // Uns 4 s com o fantasia e o manequim vestido do lado: o desenho roda
    // todo quadro, e dá tempo de tirar uma captura da tela.
    if (frames === 600) { restore(); done = true; }
});
bl.log('armor: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class ArmorTestMod extends Mod {}
