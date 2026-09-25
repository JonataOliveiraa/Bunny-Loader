// Teste das classes de mod (script/ModClasses.js): IA, spawn natural,
// Bestiario, tooltip, receita e os hooks de uso. Precisa do Example Mod ligado
// (o Bestiario e a receita dele tambem sao conferidos).
//
// Registra um item-arma, um acessorio, um projetil e um NPC proprios, cada
// hook contando quando dispara, e loga "hooks <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID, TileID } = Terraria.ID;

const count = {};
const bump = (k) => { count[k] = (count[k] || 0) + 1; };

let allowUse = true;
let spawnWeight = 0;
let lastStats = null;

class TestShot extends ModProjectile {
    SetDefaults() {
        this.Projectile.width = 8;
        this.Projectile.height = 8;
        this.Projectile.aiStyle = 0;
        this.Projectile.friendly = true;
        this.Projectile.penetrate = 1;
        this.Projectile.timeLeft = 40;
        this.Projectile.tileCollide = false;
    }
    PreAI(p) {
        bump('proj.PreAI');
        if (this.Projectile && bl.addressOf(this.Projectile) === bl.addressOf(p)) bump('proj.self');
        return true;
    }
    AI(p) { bump('proj.AI'); }
    PostAI(p) { bump('proj.PostAI'); }
    OnKill(p, timeLeft) { bump('proj.OnKill'); }
    OnHitNPC(p, npc) { bump('proj.OnHitNPC'); }
}

class TestBlob extends ModNPC {
    SetStaticDefaults() {
        Main.npcFrameCount[this.Type] = 2;
    }
    SetDefaults() {
        this.NPC.width = 24;
        this.NPC.height = 18;
        this.NPC.aiStyle = 0;
        this.NPC.damage = 0;
        this.NPC.defense = 0;
        this.NPC.lifeMax = 50;
        this.NPC.knockBackResist = 0;
        this.NPC.noGravity = true;
    }
    SetBestiary(database, entry) { bump('npc.SetBestiary'); }
    PreAI(npc) {
        bump('npc.PreAI');
        if (this.NPC && bl.addressOf(this.NPC) === bl.addressOf(npc)) bump('npc.self');
        return true;
    }
    AI(npc) { bump('npc.AI'); npc.velocity.X = 0; }
    PostAI(npc) { bump('npc.PostAI'); }
    FindFrame(npc, frameHeight) {
        bump('npc.FindFrame');
        if (frameHeight > 0) count.frameHeight = frameHeight;
    }
    CheckActive(npc) { bump('npc.CheckActive'); return false; }
    PreKill(npc) { bump('npc.PreKill'); return true; }
    OnKill(npc) { bump('npc.OnKill'); }
    SpawnChance(info) { return spawnWeight; }
}

class TestGun extends ModItem {
    constructor() {
        super();
        this.Tooltip = 'linha 1\nlinha 2';
    }
    SetDefaults() {
        this.Item.ranged = true;
        this.Item.shoot = ModProjectile.getTypeByName('TestShot');
        this.Item.shootSpeed = 6;
        this.SetWeaponValues(10, 1, 0);
        this.SetDefaultWeaponStyle(20, true);
        this.Item.noMelee = true;
    }
    ModifyTooltipLines() { this.TooltipLines.push('linha 3'); }
    AddRecipes() {
        this.CreateRecipe().AddIngredient(ItemID.Wood, 3).AddTile(TileID.WorkBenches).Register();
    }
    CanUseItem(item, player) { bump('item.CanUseItem'); return allowUse; }
    UseItem(item, player) { bump('item.UseItem'); }
    HoldItem(item, player) { bump('item.HoldItem'); }
    HoldoutOffset(item, player) { bump('item.HoldoutOffset'); return { X: -4, Y: 0 }; }
    ModifyShootStats(item, player, stats) {
        bump('item.ModifyShootStats');
        stats.damage *= 2;
        lastStats = { type: stats.type, damage: stats.damage };
    }
    Shoot(item, player, position, velocity, type, damage, knockBack) { bump('item.Shoot'); return true; }
    UpdateInventory(item, player) { bump('item.UpdateInventory'); }
}

class TestCharm extends ModItem {
    SetDefaults() {
        this.Item.accessory = true;
        this.Item.width = 20;
        this.Item.height = 20;
    }
    UpdateEquip(item, player) { bump('item.UpdateEquip'); }
    UpdateAccessory(item, player, hideVisual) { bump('item.UpdateAccessory'); }
}

const SHOT = ModProjectile.register(TestShot);
const BLOB = ModNPC.register(TestBlob);
const GUN = ModItem.register(TestGun);
const CHARM = ModItem.register(TestCharm);

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('hooks ' + label + ': ok');
        else { fails++; bl.log('hooks ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('hooks ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const got = (k, min = 1) => (count[k] || 0) >= min || `${k} = ${count[k] || 0}`;

const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
let source = null;
let blob = null, gunSlot = -1, savedSlot = null, savedSelected = 0;

function me() { return Main.player[Main.myPlayer]; }

// O slime do Example Mod pelo nome: typeOf so ve os NPCs deste mod.
function exampleSlimeType() {
    for (let t = bl.npcs.vanillaCount; t < bl.npcs.vanillaCount + 16; t++) {
        if (!bl.npcs.isModNpc(t)) break;
        if (/Slime de Exemplo|Example Slime/.test(Terraria.Lang['string GetNPCNameValue(int netID)'](t))) return t;
    }
    return -1;
}

function setup() {
    source = Terraria.DataStructures.EntitySource_DebugCommand.new();
    source['void .ctor()']();
    const p = me();

    check('tooltip', () => {
        const tip = Terraria.Lang['ItemTooltip GetTooltip(int itemId)'](GUN);
        const lines = [];
        for (let i = 0; i < tip.Lines; i++) lines.push(tip.GetLine(i));
        return lines.join('|') === 'linha 1|linha 2|linha 3' || 'linhas: ' + lines.join('|');
    });
    check('instancia por item', () => {
        const a = Terraria.Item.new();
        a['void .ctor()']();
        a['void SetDefaults(int Type, ItemVariant variant)'](GUN, null);
        const b = Terraria.Item.new();
        b['void .ctor()']();
        b['void SetDefaults(int Type, ItemVariant variant)'](GUN, null);
        const ma = a.ModItem, mb = b.ModItem;
        if (!ma || !mb) return 'item.ModItem vazio';
        if (ma === mb) return 'os dois itens dividem a instancia';
        if (!(ma instanceof TestGun)) return 'instancia nao e TestGun';
        if (ma === ModItem.getModItem(GUN)) return 'item.ModItem e o molde';
        if (bl.addressOf(ma.Item) !== bl.addressOf(a)) return 'this.Item nao e o item';
        ma.charge = 7;
        if (mb.charge !== undefined) return 'estado vazou para outro item';
        const c = a.Clone();
        const mc = c.ModItem;
        if (!mc || mc === ma) return 'Clone nao ganhou instancia propria';
        if (mc.charge !== 7) return 'Clone perdeu o estado: ' + mc.charge;
        return bl.addressOf(mc.Item) === bl.addressOf(c) || 'this.Item do clone errado';
    });
    check('campo extra do mod', () => {
        bl.defineField(Terraria.Player, 'testeContador');
        const p = me();
        p.testeContador = 41;
        p.testeContador++;
        return p.testeContador === 42 || 'valor ' + p.testeContador;
    });
    check('receita', () => {
        const recipes = Main.recipe;
        let gun = false, example = false;
        for (let i = Terraria.Recipe.numRecipes - 1; i >= Math.max(0, Terraria.Recipe.numRecipes - 40); i--) {
            const it = recipes[i].createItem;
            if (it.type === GUN) gun = true;
            else if (bl.items.isModItem(it.type) && it.stack === 999) example = true;
        }
        return (gun && example) || `arma de teste=${gun} ExampleItem=${example}`;
    });
    check('Bestiario', () => {
        const db = Main.BestiaryDB;
        const mine = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](BLOB);
        const slime = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](exampleSlimeType());
        if (!got('npc.SetBestiary')) return 'SetBestiary nao rodou';
        if (!mine || mine.Info.Count === 0) return 'sem entrada para o NPC de teste';
        return (slime && slime.Info.Count >= 4) || 'entrada do slime: ' + (slime ? slime.Info.Count + ' elementos' : 'nenhuma');
    });

    // A arma no inventario, o acessorio equipado.
    // Na barra rapida (o HoldItem precisa dela na mao); sem espaco, o 9 fica
    // emprestado e volta no fim.
    for (let i = 0; i < 10; i++) {
        if (p.inventory[i].type === 0) { gunSlot = i; break; }
    }
    if (gunSlot < 0) {
        gunSlot = 9;
        const it = p.inventory[9];
        savedSlot = { type: it.type, stack: it.stack, prefix: it.prefix };
    }
    p.inventory[gunSlot]['void SetDefaults(int Type, ItemVariant variant)'](GUN, null);
    p.armor[3]['void SetDefaults(int Type, ItemVariant variant)'](CHARM, null);

    const i = newNpc(source, Math.floor(p.position.X + 200), Math.floor(p.position.Y), BLOB, 0, 0, 0, 0, 0, 255);
    blob = Main.npc[i];
    const c = blob.Center;
    newProj(source, c.X - 80, c.Y - 4, 4, 0, SHOT, 5, 0, Main.myPlayer, 0, 0, 0, null);
}

function useChecks() {
    const p = me();
    const gun = p.inventory[gunSlot];
    const canUse = (item, ignoreCursed) => p['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'](item, ignoreCursed);
    check('CanUseItem', () => {
        allowUse = false;
        const blocked = canUse(gun, false);
        allowUse = true;
        return (got('item.CanUseItem') === true && blocked === false) || `bloqueado devolveu ${blocked}`;
    });
    check('Shoot e ModifyShootStats', () => {
        const before = count['item.Shoot'] || 0;
        p['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'](Main.myPlayer, gun, 10, false);
        if ((count['item.Shoot'] || 0) <= before) return 'Shoot nao rodou';
        return (lastStats && lastStats.type === SHOT && lastStats.damage === 20) || 'stats ' + JSON.stringify(lastStats);
    });
    check('UseItem', () => {
        p['void ItemCheck_StartActualUse(Item sItem)'](gun);
        return got('item.UseItem');
    });
    savedSelected = p.selectedItemState.selected;
    p.selectedItemState.selected = gunSlot;
}

function finalChecks() {
    check('UpdateInventory', () => got('item.UpdateInventory', 10));
    check('UpdateAccessory e UpdateEquip', () => got('item.UpdateAccessory', 10) === true ? got('item.UpdateEquip', 10) : got('item.UpdateAccessory', 10));
    check('projetil: PreAI, AI, PostAI', () => {
        const r = [got('proj.PreAI', 5), got('proj.AI', 5), got('proj.PostAI', 5)].find(x => x !== true);
        return r === undefined ? true : r;
    });
    check('projetil: OnKill', () => got('proj.OnKill'));
    check('projetil: this.Projectile', () => got('proj.self', 5));
    check('NPC: this.NPC e npc.ModNPC', () => {
        if (got('npc.self', 5) !== true) return got('npc.self', 5);
        const m = blob.ModNPC;
        return (m instanceof TestBlob && m !== ModNPC.getModNPC(BLOB)) || 'npc.ModNPC ' + m;
    });
    check('projetil: OnHitNPC', () => got('proj.OnHitNPC'));
    check('NPC: PreAI, AI, PostAI', () => {
        const r = [got('npc.PreAI', 30), got('npc.AI', 30), got('npc.PostAI', 30)].find(x => x !== true);
        return r === undefined ? true : r;
    });
    check('NPC: FindFrame', () => got('npc.FindFrame', 10) === true ? (count.frameHeight > 0 || 'frameHeight 0') : got('npc.FindFrame', 10));
    check('NPC: CheckActive', () => got('npc.CheckActive', 10));
    check('HoldItem e HoldoutOffset', () => got('item.HoldItem') === true ? got('item.HoldoutOffset') : got('item.HoldItem'));
}

function killChecks() {
    check('NPC: PreKill e OnKill', () => {
        blob.playerInteraction[Main.myPlayer] = true;
        blob['double StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool noEffect, bool fromNet, int owner)'](
            9999, 0, 1, false, false, false, Main.myPlayer);
        return got('npc.PreKill') === true ? got('npc.OnKill') : got('npc.PreKill');
    });
}

function spawnChecks() {
    check('spawn natural', () => {
        spawnWeight = 1e6;
        const before = countBlobs();
        const spawn = Terraria.NPC['void SpawnNPC()'];
        for (let i = 0; i < 3000 && countBlobs() === before; i++) spawn();
        spawnWeight = 0;
        const after = countBlobs();
        return after > before || `nenhum NPC de teste nasceu (${before} -> ${after})`;
    });
}

function countBlobs() {
    let n = 0;
    const npcs = Main.npc;
    for (let i = 0; i < 200; i++) if (npcs[i].active && npcs[i].type === BLOB) n++;
    return n;
}

function cleanup() {
    const p = me();
    p.selectedItemState.selected = savedSelected;
    if (savedSlot) {
        const it = p.inventory[9];
        it['void SetDefaults(int Type, ItemVariant variant)'](savedSlot.type, null);
        it.stack = savedSlot.stack;
        it.prefix = savedSlot.prefix;
    } else if (gunSlot >= 0) {
        p.inventory[gunSlot].TurnToAir(false);
    }
    p.armor[3].TurnToAir(false);
    const npcs = Main.npc;
    for (let i = 0; i < 200; i++) if (npcs[i].active && npcs[i].type === BLOB) npcs[i].active = false;
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 60) setup();
    if (frames === 90) useChecks();
    if (frames === 150) finalChecks();
    if (frames === 160) killChecks();
    if (frames === 170) spawnChecks();
    if (frames === 180) {
        cleanup();
        bl.log('hooks FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log(`hooks: carregado (arma ${GUN}, acessorio ${CHARM}, projetil ${SHOT}, NPC ${BLOB})`);
