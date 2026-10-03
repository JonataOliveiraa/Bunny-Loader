// DamageClass. Os bônus do jogador são postos a cada quadro no
// PostUpdateEquips (como um acessório), conforme o passo; as medidas saem no
// fim do Player.Update, depois da dobra nos campos do jogo.
// Loga "damageclass <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID, ProjectileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('damageclass ' + label + ': ok');
        else { fails++; bl.log('damageclass ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('damageclass ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// Herda o Generic, conta como mágica; 4 de crítico e 10 de penetração a mais.
export class TestClass extends DamageClass {
    DisplayName = 'test damage';
    GetEffectInheritance(c) { return c === DamageClass.Magic; }
    SetDefaultStats(player) {
        player.GetCritChance(this).value += 4;
        player.GetArmorPenetration(this).value += 10;
    }
}

export class TestBlade extends ModItem {
    SetDefaults() {
        this.Item.DamageType = ModContent.GetInstance(TestClass);
        this.Item.width = this.Item.height = 20;
        this.Item.useStyle = 1;
        this.Item.useTime = this.Item.useAnimation = 20;
        this.Item.damage = 100;
        this.Item.knockBack = 4;
        this.Item.crit = 6;
    }
}

// Sentinela parada da classe de teste: o dano dela o jogo refaz todo quadro
// (originalDamage), e o crítico é sorteado de novo pela classe.
export class TestOrb extends ModProjectile {
    SetDefaults() {
        this.Projectile.DamageType = ModContent.GetInstance(TestClass);
        this.Projectile.width = this.Projectile.height = 40;
        this.Projectile.aiStyle = -1;
        this.Projectile.friendly = true;
        this.Projectile.sentry = true;
        this.Projectile.tileCollide = false;
        this.Projectile.penetrate = -1;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = 2;
        this.Projectile.timeLeft = 6000;
    }
}

// Troca a classe de um item do jogo (a Espada de Madeira vira TestClass).
export class TestGlobal extends GlobalItem {
    SetDefaults(item) {
        if (item.type === ItemID.WoodenSword) item.DamageType = ModContent.GetInstance(TestClass);
    }
}

let bonus = null;
let equipCalls = 0;      // (player) => void, posto todo quadro
let modify = null;     // (damage) => número ou nada
export class TestDCPlayer extends ModPlayer {
    PostUpdateEquips(player) {
        if (player.whoAmI !== Main.myPlayer) return;
        equipCalls++;
        if (bonus) bonus(player);
    }
    ModifyWeaponDamage(player, item, damage) { if (modify && item.type === ItemID.IronBroadsword) return modify(damage); }
}

const sample = (type) => {
    const item = Terraria.Item.new();
    item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
};
const weaponDamage = (p, item) => p['int GetWeaponDamage(Item sItem)'](item);
const weaponCrit = (p, item) => p['int GetWeaponCrit(Item sItem)'](item);
const weaponKb = (p, item) => p['float GetWeaponKnockback(Item sItem, float KnockBack)'](item, item.knockBack);

const steps = [];
const step = (setup, measure) => steps.push({ setup, measure });
const base = {};
let speedSeen = -1;

step(null, (p) => {
    const V = DamageClass;
    check('as 10 do jogo, na ordem do tModLoader', () => {
        const names = [V.Default, V.Generic, V.Melee, V.MeleeNoSpeed, V.Ranged, V.Magic, V.Summon,
                       V.SummonMeleeSpeed, V.MagicSummonHybrid, V.Throwing].map((c) => c.Type).join(',');
        return names === '0,1,2,3,4,5,6,7,8,9' || names;
    });
    check('a de mod vem depois (Type 10+)', () => ModContent.GetInstance(TestClass).Type >= 10 || ModContent.GetInstance(TestClass).Type);
    // No celular só o item 3821 tem NoMeleeSpeedBonus, e ele não é corpo a
    // corpo: a espada marcada por um instante faz o papel.
    check('classe pelas flags: espada Melee, sem velocidade MeleeNoSpeed, pistola Ranged, cajado Magic, chicote SummonMeleeSpeed', () => {
        const S = ItemID.Sets;
        S.NoMeleeSpeedBonus[ItemID.IronShortsword] = true;
        const noSpeed = sample(ItemID.IronShortsword).DamageType;
        S.NoMeleeSpeedBonus[ItemID.IronShortsword] = false;
        const got = [ItemID.IronBroadsword, ItemID.FlintlockPistol, ItemID.AmethystStaff, ItemID.BlandWhip]
            .map((t) => sample(t).DamageType);
        got.splice(1, 0, noSpeed);
        const want = [V.Melee, V.MeleeNoSpeed, V.Ranged, V.Magic, V.SummonMeleeSpeed];
        return got.every((c, i) => c === want[i]) || got.map((c) => DamageClassLoader.NameOf(c)).join(',');
    });
    check('DamageType de mod: a classe volta, e as flags são as do "conta como" (magic)', () => {
        const b = sample(ModContent.ItemType(TestBlade));
        return (b.DamageType === ModContent.GetInstance(TestClass) && b.magic && !b.melee && !b.ranged && !b.summon) ||
            `${DamageClassLoader.NameOf(b.DamageType)} melee ${b.melee} magic ${b.magic}`;
    });
    check('a classe sobrevive ao Clone', () => sample(ModContent.ItemType(TestBlade)).Clone().DamageType === ModContent.GetInstance(TestClass));
    check('CountsAsClass: Magic sim, Melee não', () => {
        const b = sample(ModContent.ItemType(TestBlade));
        return (b.CountsAsClass(DamageClass.Magic) && !b.CountsAsClass(DamageClass.Melee)) || 'errado';
    });
    check('DamageType = classe do jogo liga só a flag (Ranged)', () => {
        const it = sample(ItemID.IronBroadsword);
        it.DamageType = DamageClass.Ranged;
        return (it.ranged && !it.melee && it.DamageType === DamageClass.Ranged) || `melee ${it.melee} ranged ${it.ranged}`;
    });
    check('GlobalItem troca a classe de um item do jogo', () =>
        sample(ItemID.WoodenSword).DamageType === ModContent.GetInstance(TestClass) || DamageClassLoader.NameOf(sample(ItemID.WoodenSword).DamageType));
    check('projétil: DamageType e flags', () => {
        const pr = Terraria.Projectile.new();
        pr['void .ctor()']();
        pr['void SetDefaults(int Type)'](ProjectileID.WoodenArrowFriendly);
        const ranged = pr.DamageType === DamageClass.Ranged;
        pr.DamageType = ModContent.GetInstance(TestClass);
        return (ranged && pr.magic && !pr.ranged && pr.DamageType === ModContent.GetInstance(TestClass)) || `ranged ${ranged} magic ${pr.magic}`;
    });
    check('GetArmorPenetration(bool) do jogo segue funcionando', () => typeof p.GetArmorPenetration(true) === 'number');

    base.sword = weaponDamage(p, sample(ItemID.IronBroadsword));
    base.blade = weaponDamage(p, sample(ModContent.ItemType(TestBlade)));
    base.bladeCrit = weaponCrit(p, sample(ModContent.ItemType(TestBlade)));
    base.meleeCrit = p.meleeCrit;
    base.bladeKb = weaponKb(p, sample(ModContent.ItemType(TestBlade)));
    bl.log(`damageclass diag: espada ${base.sword}, lâmina ${base.blade}, crítico da lâmina ${base.bladeCrit}, meleeCrit ${base.meleeCrit}`);
    check('crítico da classe de mod: o comum do jogo (4) + 4 da classe + 6 do item', () => base.bladeCrit === Math.min(p.meleeCrit, p.rangedCrit, p.magicCrit) + 4 + 6 || `${base.bladeCrit} (crits ${p.meleeCrit}/${p.rangedCrit}/${p.magicCrit})`);
});

step((p) => { p.GetDamage(DamageClass.Generic).Additive += 0.5; }, (p) => {
    const sword = weaponDamage(p, sample(ItemID.IronBroadsword));
    const blade = weaponDamage(p, sample(ModContent.ItemType(TestBlade)));
    bl.log(`damageclass diag: Generic +50%: espada ${sword}, lâmina ${blade}`);
    check('Generic +50%: a espada do jogo sobe (campo dobrado)', () => sword > base.sword || `${base.sword} -> ${sword}`);
    check('Generic +50%: a classe de mod herda', () => Math.abs(blade - Math.trunc(base.blade * 1.5)) <= 1 || `${base.blade} -> ${blade}`);
});

step((p) => { p.GetDamage(TestClass).Additive += 1; }, (p) => {
    const sword = weaponDamage(p, sample(ItemID.IronBroadsword));
    const blade = weaponDamage(p, sample(ModContent.ItemType(TestBlade)));
    check('dano só da classe de mod: a lâmina dobra, a espada não muda', () =>
        (Math.abs(blade - base.blade * 2) <= 1 && sword === base.sword) || `lâmina ${blade}, espada ${sword}`);
});

step((p) => { p.GetDamage(DamageClass.Generic).Base += 10; p.GetDamage(DamageClass.Generic).Flat += 5; }, (p) => {
    const sword = sample(ItemID.IronBroadsword);
    const got = weaponDamage(p, sword);
    const mult = p.meleeDamage;
    const want = Math.trunc((sword.damage + 10) * mult + 5);
    check('Base e Flat (Generic) no item do jogo', () => Math.abs(got - want) <= 1 || `${got}, esperado ${want}`);
});

step((p) => { p.GetCritChance(DamageClass.Melee).value += 10; p.GetCritChance(DamageClass.Generic).value += 5; }, (p) => {
    check('crítico Melee +10 e Generic +5 vão para o meleeCrit', () => p.meleeCrit === base.meleeCrit + 15 || `${base.meleeCrit} -> ${p.meleeCrit}`);
    const blade = weaponCrit(p, sample(ModContent.ItemType(TestBlade)));
    check('a classe de mod herda só o Generic (+5), não o Melee', () => blade === base.bladeCrit + 5 || `${base.bladeCrit} -> ${blade}`);
});

step((p) => { p.GetKnockback(TestClass).Additive += 1; }, (p) => {
    const kb = weaponKb(p, sample(ModContent.ItemType(TestBlade)));
    check('repulsão da classe de mod dobra', () => Math.abs(kb - base.bladeKb * 2) < 0.01 || `${base.bladeKb} -> ${kb}`);
});

step((p) => { p.GetAttackSpeed(DamageClass.Ranged).value += 1; speedSeen = p.GetAttackSpeed(DamageClass.Ranged).value; }, (p) => {
    bl.log(`damageclass diag: PostUpdateEquips ${equipCalls} vezes, logo depois de somar ${speedSeen}`);
    const gun = sample(ItemID.FlintlockPistol);
    bl.log(`damageclass diag: velocidade Ranged guardada ${p.GetAttackSpeed(DamageClass.Ranged).value}, total ${p.GetTotalAttackSpeed(DamageClass.Ranged)}, classe ${DamageClassLoader.NameOf(gun.DamageType)}`);
    p['void ApplyItemTime(Item sItem)'](gun);
    const t = p.itemTime;
    p.itemTime = 0;
    check('velocidade Ranged x2: o tempo de tiro cai pela metade', () => t === Math.max(1, Math.trunc(gun.useTime / 2)) || `useTime ${gun.useTime}, itemTime ${t}`);
});

// Os acertos no slime alvo: quantos e quantos críticos.
let target = null, orb = null;
const strikes = { hits: 0, crits: 0 };
Terraria.NPC['int StrikeNPC(int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)'].hook(
    (original, npc, damage, kb, dir, crit, fromNet, owner) => {
        if (target && npc.whoAmI === target.whoAmI) {
            strikes.hits++;
            if (crit) strikes.crits++;
        }
        return original(npc, damage, kb, dir, crit, fromNet, owner);
    });

function arena(p) {
    if (target && target.active && orb && orb.active) {
        orb.Center = Vector2.new(target.Center.X, target.Center.Y);
        orb.velocity = Vector2.new(0, 0);
        target.velocity = Vector2.new(0, 0);
        target.life = target.lifeMax;
        return;
    }
    const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
    const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
    const src = Terraria.DataStructures.EntitySource_DebugCommand.new();
    target = Main.npc[newNpc(src, Math.floor(p.Center.X) + 8 * 16, Math.floor(p.position.Y), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer)];
    target.lifeMax = target.life = 500000;
    target.damage = 0;
    orb = Main.projectile[newProj(src, target.Center.X, target.Center.Y, 0, 0, ModContent.ProjectileType(TestOrb), 100, 0, Main.myPlayer, 0, 0, 0, null)];
}

step((p) => { arena(p); p.GetDamage(TestClass).Additive += 1; p.GetCritChance(TestClass).value += 100; }, (p) => {
    check('sentinela da classe de mod: o dano refeito pela classe (100 -> 200)', () =>
        (orb && orb.originalDamage === 100 && orb.damage === 200) || `original ${orb && orb.originalDamage}, dano ${orb && orb.damage}`);
    strikes.hits = strikes.crits = 0;
});
step((p) => { arena(p); p.GetCritChance(TestClass).value += 100; }, (p) => {
    bl.log(`damageclass diag: crítico +100: ${strikes.hits} acertos, ${strikes.crits} críticos`);
    check('projétil da classe de mod: crítico da classe (+100) acerta crítico sempre', () =>
        (strikes.hits > 0 && strikes.crits === strikes.hits) || `${strikes.crits} de ${strikes.hits}`);
    strikes.hits = strikes.crits = 0;
});
step((p) => { arena(p); p.GetCritChance(TestClass).value -= 200; }, (p) => {
    bl.log(`damageclass diag: crítico -200: ${strikes.hits} acertos, ${strikes.crits} críticos`);
    check('projétil da classe de mod: crítico negativo nunca acerta crítico (o sorteio do jogo é refeito)', () =>
        (strikes.hits > 0 && strikes.crits === 0) || `${strikes.crits} de ${strikes.hits}`);
    if (orb) orb.active = false;
    if (target) target.active = false;
});

let penProj = -1;
step((p) => { p.GetArmorPenetration(DamageClass.Magic).value += 5; }, (p) => {
    bl.log(`damageclass diag: penetração Magic guardada ${p.GetArmorPenetration(DamageClass.Magic).value}, total ${p.GetTotalArmorPenetration(DamageClass.Magic)}, armorPenetration ${p.armorPenetration}`);
    const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
    const src = Terraria.DataStructures.EntitySource_DebugCommand.new();
    const magic = newProj(src, p.Center.X, p.Center.Y - 200, 0, 0, ProjectileID.AmethystBolt, 10, 0, Main.myPlayer, 0, 0, 0, null);
    const arrow = newProj(src, p.Center.X, p.Center.Y - 200, 0, 0, ProjectileID.WoodenArrowFriendly, 10, 0, Main.myPlayer, 0, 0, 0, null);
    const a = Main.projectile[magic].armorPenetration, b = Main.projectile[arrow].armorPenetration;
    Main.projectile[magic].active = false;
    Main.projectile[arrow].active = false;
    check('penetração Magic +5 entra no projétil mágico, não na flecha', () => (a === 5 && b === 0) || `mágico ${a}, flecha ${b}`);
});

step(null, (p) => {
    modify = (damage) => damage * 2;
    const legacy = weaponDamage(p, sample(ItemID.IronBroadsword));
    modify = (damage) => { damage.Additive += 1; };
    const modern = weaponDamage(p, sample(ItemID.IronBroadsword));
    modify = null;
    check('ModifyWeaponDamage devolvendo número (o de antes)', () => legacy === base.sword * 2 || `${base.sword} -> ${legacy}`);
    check('ModifyWeaponDamage mexendo no StatModifier (o do tModLoader)', () => modern === base.sword * 2 || `${base.sword} -> ${modern}`);
});

let frames = 0, current = -1, wait = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    // Vivo e de dia: morto, o jogo pula o UpdateEquips (e os bônus do passo).
    self.statLife = self.statLifeMax2;
    if (frames === 0) { Main.dayTime = true; Main.time = 20000; }
    if (++frames < 60) return;

    if (current < 0 || wait <= 0) {
        if (current >= 0) {
            try { steps[current].measure(self); } catch (e) { fails++; bl.log('damageclass passo ' + current + ': FALHOU com ' + e + ' | ' + (e.stack || '')); }
        }
        current++;
        if (current >= steps.length) {
            bonus = null;
            done = true;
            bl.log('damageclass FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            return;
        }
        bonus = steps[current].setup;
        wait = 3;
        return;
    }
    wait--;
});
bl.log('damageclass: carregado');

export default class TestDamageClass extends Mod {}
