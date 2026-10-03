// DamageClass, parte 2. Bônus postos a cada quadro no PostUpdateEquips;
// medidas no fim do Player.Update. O tooltip é o desenhado de verdade: o
// DrawPendingMouseText põe a arma no HoverItem (como o tooltipdraw).
// Loga "damageclass2 <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID, NPCID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('damageclass2 ' + label + ': ok');
        else { fails++; bl.log('damageclass2 ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('damageclass2 ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// Herda tudo do Generic; esconde a linha de velocidade; 10 de penetração.
export class TipClass extends DamageClass {
    DisplayName = 'tip damage';
    SetDefaultStats(player) { player.GetArmorPenetration(this).value += 10; }
    ShowStatTooltipLine(player, line) { return line !== 'Speed'; }
}

export class NoCritClass extends DamageClass {
    get UseStandardCritCalcs() { return false; }
}

// Metade do dano à distância, tudo do Generic.
export class HalfRangedClass extends DamageClass {
    GetModifierInheritance(c) {
        if (c === DamageClass.Generic) return StatInheritanceData.Full;
        if (c === DamageClass.Ranged) return new StatInheritanceData({ damageInheritance: 0.5 });
        return StatInheritanceData.None;
    }
}

const tips = new Map();   // nome da linha -> texto, do último tooltip desenhado

const weapon = (item, cls, damage = 100) => {
    item.DamageType = cls;
    item.width = item.height = 20;
    item.useStyle = 1;
    item.useTime = item.useAnimation = 20;
    item.damage = damage;
    item.knockBack = 4;
};

export class TipBlade extends ModItem {
    SetDefaults() { weapon(this.Item, ModContent.GetInstance(TipClass)); }
    ModifyTooltips(item, lines) {
        tips.clear();
        for (const l of lines) tips.set(l.Name, String(l.Text));
    }
}
export class NoCritBlade extends ModItem {
    SetDefaults() { weapon(this.Item, ModContent.GetInstance(NoCritClass)); }
    ModifyTooltips(item, lines) {
        tips.clear();
        for (const l of lines) tips.set(l.Name, String(l.Text));
    }
}
export class HalfGun extends ModItem { SetDefaults() { weapon(this.Item, ModContent.GetInstance(HalfRangedClass)); } }
export class ThrowKnife extends ModItem { SetDefaults() { weapon(this.Item, DamageClass.Throwing); } }
export class SpearLike extends ModItem { SetDefaults() { weapon(this.Item, DamageClass.MeleeNoSpeed); } }
export class WhipLike extends ModItem { SetDefaults() { weapon(this.Item, DamageClass.SummonMeleeSpeed); } }

let bonus = null;
export class TestDC2Player extends ModPlayer {
    PostUpdateEquips(player) { if (bonus && player.whoAmI === Main.myPlayer) bonus(player); }
}

const sample = (type) => {
    const item = Terraria.Item.new();
    item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
};
const T = (cls) => ModContent.ItemType(cls);
const damageOf = (p, item) => p['int GetWeaponDamage(Item sItem)'](item);

// O tooltip desenhado de verdade: `hover` fica no HoverItem enquanto não é null.
let hover = null;
Terraria.Main['void DrawPendingMouseText(bool worldMouse)'].hook((original, worldMouse) => {
    if (hover && !worldMouse) {
        Main.HoverItem = hover['Item Clone()']();
        Main.inventoryTooltipTime = 30;
        Main.instance['void MouseText(string cursorText, int rare, byte diff, int hackedMouseX, int hackedMouseY, int hackedScreenWidth, int hackedScreenHeight, int pushWidthX)'](
            hover.Name, hover.rare, 0, -1, -1, -1, -1, 0);
    }
    return original();
});

// Os acertos no slime alvo (o golpe corpo a corpo direto).
let target = null;
const strikes = [];
Terraria.NPC['int StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)'].hook(
    (original, npc, damage, kb, dir, crit, fromNet, owner) => {
        if (target && npc.whoAmI === target.whoAmI) strikes.push(damage);
        return original(npc, damage, kb, dir, crit, fromNet, owner);
    });

const steps = [];
const step = (setup, measure, wait = 3) => steps.push({ setup, measure, wait });

step(null, (p) => {
    check('MeleeNoSpeed atribuída: flag melee e NoMeleeSpeedBonus do tipo', () => {
        const it = sample(T(SpearLike));
        return (it.melee && ItemID.Sets.NoMeleeSpeedBonus[it.type] && it.DamageType === DamageClass.MeleeNoSpeed) || `melee ${it.melee}`;
    });
    check('SummonMeleeSpeed atribuída: flag summon e o set do chicote', () => {
        const it = sample(T(WhipLike));
        return (it.summon && ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[it.type] && it.DamageType === DamageClass.SummonMeleeSpeed) || `summon ${it.summon}`;
    });
    check('Throwing: classe de gancho, flag ranged pelos prefixos', () => {
        const it = sample(T(ThrowKnife));
        return (DamageClassLoader.IsHooked(DamageClass.Throwing) && it.ranged && !it.melee) || `ranged ${it.ranged}`;
    });
    check('classe sem crítico: GetWeaponCrit 0', () => p['int GetWeaponCrit(Item sItem)'](sample(T(NoCritBlade))) === 0);

    const ex = ModContent.ItemType('ExampleCustomDamageWeapon');
    check('Example Mod: ExampleCustomDamageWeapon com a ExampleDamageClass, flags melee e magic', () => {
        if (ex <= 0) return 'sem o item';
        const it = sample(ex);
        return (DamageClassLoader.NameOf(it.DamageType) === 'ExampleDamageClass' && it.melee && it.magic && !it.ranged) ||
            `${DamageClassLoader.NameOf(it.DamageType)} melee ${it.melee} magic ${it.magic}`;
    });
    check('Example Mod: crítico da arma = comum do jogo + 4 da classe + 6 do item', () => {
        const c = p['int GetWeaponCrit(Item sItem)'](sample(ex));
        const common = Math.min(p.meleeCrit, p.rangedCrit, p.magicCrit);
        return c === common + 4 + 6 || `${c} (comum ${common})`;
    });
    check('Example Mod: a arma aceita prefixo (as flags dão a categoria)', () => {
        for (let k = 0; k < 40; k++) {
            const it = sample(ex);
            it['bool Prefix(int prefixWeWant)'](-1);
            if (it.prefix > 0) return true;
        }
        return 'nenhum prefixo em 40 tentativas';
    });
    hover = sample(T(TipBlade));
}, 20);

step(null, (p) => {
    hover = null;
    bl.log('damageclass2 diag: tooltip ' + JSON.stringify([...tips]));
    check('tooltip: "X tip damage" na linha Damage', () => /^\d+ tip damage$/.test(tips.get('Damage') || '') || tips.get('Damage'));
    check('tooltip: crítico da classe', () => tips.has('CritChance') || 'sem CritChance');
    check('tooltip: ShowStatTooltipLine esconde a velocidade', () => !tips.has('Speed') || tips.get('Speed'));
    check('tooltip: repulsão aparece', () => tips.has('Knockback') || 'sem Knockback');
    hover = sample(T(NoCritBlade));
}, 20);

step(null, (p) => {
    hover = null;
    check('tooltip: classe sem crítico não tem a linha CritChance', () => (tips.has('Damage') && !tips.has('CritChance')) || JSON.stringify([...tips]));
    // A arma do Example Mod, sem ModifyTooltips: fica na tela para a captura.
    const ex = ModContent.ItemType('ExampleCustomDamageWeapon');
    if (ex > 0) hover = sample(ex);
}, 120);

step((p) => { p.GetAttackSpeed(TipClass).value += 1; }, (p) => {
    hover = null;
    const blade = sample(T(TipBlade));
    p['void ApplyItemAnimation(Item sItem)'](blade);
    const frames = p.itemAnimationMax;
    p.itemAnimation = p.itemAnimationMax = 0;
    check('velocidade da classe de mod x2: a animação cai pela metade', () => frames === blade.useAnimation / 2 || `useAnimation ${blade.useAnimation}, animação ${frames}`);
});

step((p) => { p.GetDamage(DamageClass.Ranged).Additive += 1; }, (p) => {
    const got = damageOf(p, sample(T(HalfGun)));
    check('herança parcial: metade do +100% à distância (100 -> 150)', () => got === 150 || got);
});

step((p) => { p.GetDamage(DamageClass.Throwing).Additive += 1; p.GetDamage(DamageClass.Generic).Additive += 0.5; }, (p) => {
    const got = damageOf(p, sample(T(ThrowKnife)));
    check('Throwing: o dele (+100%) e o do Generic (+50%) somam (100 -> 250)', () => got === 250 || got);
});

// Golpes diretos num slime de defesa 20: a lâmina tem 10 de penetração da
// classe. O jogo varia o dano (~15%): média de 40 golpes de cada lado, sem crítico.
function meleeAverage(p) {
    const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
    target = Main.npc[newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 4 * 16,
                             Math.floor(p.position.Y), NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer)];
    target.lifeMax = target.life = 500000;
    target.damage = 0;
    target.defense = 20;
    const blade = sample(T(TipBlade));
    const rect = Rectangle.new(Math.floor(target.position.X), Math.floor(target.position.Y), target.width, target.height);
    const before = strikes.length;
    for (let k = 0; k < 40; k++) {
        target.immune[Main.myPlayer] = 0;
        target.life = target.lifeMax;
        p['void ProcessHitAgainstNPC(Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)'](blade, rect, 100, 0, target.whoAmI);
    }
    const got = strikes.slice(before);
    target.active = false;
    return got.length ? got.reduce((a, b) => a + b, 0) / got.length : null;
}

let penHits = null;
step((p) => { p.GetCritChance(TipClass).value -= 100; }, (p) => {
    penHits = { avg: meleeAverage(p), armorAfter: p.armorPenetration };
    bl.log(`damageclass2 diag: média com 10 de penetração: ${penHits.avg}, armorPenetration depois ${p.armorPenetration}`);
});

step((p) => { p.GetCritChance(TipClass).value -= 100; p.GetArmorPenetration(TipClass).value -= 10; }, (p) => {
    const noPen = meleeAverage(p);
    bl.log(`damageclass2 diag: média sem penetração: ${noPen}`);
    check('golpe corpo a corpo: a penetração da classe chega ao acerto (média ~5 a mais)', () =>
        (penHits && penHits.avg !== null && noPen !== null && penHits.avg - noPen > 2.5) || `${penHits && penHits.avg} vs ${noPen}`);
    check('golpe corpo a corpo: armorPenetration do jogador volta ao normal', () => penHits.armorAfter === p.armorPenetration || `${penHits.armorAfter} vs ${p.armorPenetration}`);
});

// O acessório portado do Example Mod, num espaço de acessório.
let accSlot = 3, accSaved = 0;
step(null, (p) => {
    base.meleeCrit = p.meleeCrit;
    base.sword = damageOf(p, sample(ItemID.IronBroadsword));
    accSaved = p.armor[accSlot].type;
    p.armor[accSlot]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('ExampleStatBonusAccessory'), null);
}, 3);
step(null, (p) => {
    const sword = damageOf(p, sample(ItemID.IronBroadsword));
    const want = Math.trunc((sample(ItemID.IronBroadsword).damage + 4) * p.meleeDamage + 5);
    bl.log(`damageclass2 diag: acessório: meleeCrit ${base.meleeCrit} -> ${p.meleeCrit}, espada ${base.sword} -> ${sword} (esperado ${want})`);
    check('Example Mod: acessório dá +10% de crítico corpo a corpo', () => p.meleeCrit === base.meleeCrit + 10 || `${base.meleeCrit} -> ${p.meleeCrit}`);
    check('Example Mod: acessório (+25%, x1,12, base +4, +5) na espada do jogo', () => Math.abs(sword - want) <= 1 && sword > base.sword || `${sword}, esperado ${want}`);
    p.armor[accSlot]['void SetDefaults(int Type, ItemVariant variant)'](accSaved, null);
});

const base = {};
let frames = 0, current = -1, wait = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    if (frames === 0) { Main.dayTime = true; Main.time = 20000; }
    if (++frames < 60) return;

    if (current < 0 || wait <= 0) {
        if (current >= 0) {
            try { steps[current].measure(self); } catch (e) { fails++; bl.log('damageclass2 passo ' + current + ': FALHOU com ' + e + ' | ' + (e.stack || '')); }
        }
        current++;
        if (current >= steps.length) {
            bonus = null;
            // A arma do Example Mod (sem ModifyTooltips) fica no tooltip para a captura.
            const ex = ModContent.ItemType('ExampleCustomDamageWeapon');
            hover = ex > 0 ? sample(ex) : null;
            done = true;
            bl.log('damageclass2 FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            return;
        }
        bonus = steps[current].setup;
        wait = steps[current].wait;
        return;
    }
    wait--;
});
bl.log('damageclass2: carregado');

export default class TestDamageClass2 extends Mod {}
