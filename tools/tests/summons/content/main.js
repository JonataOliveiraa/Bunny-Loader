// Pets, lacaio e sentinela do Example Mod, usados como o dedo usaria (o
// controlUseItem do ItemCheck) e pelo slot de pet do equipamento:
//   pet pelo item: o buff entra, o BuffHandle_SpawnPetIfNeededAndSetTime (com
//     ref bool) cria o pet e poe o buff em 18000; o pet segue o jogador;
//   pet pelo slot de pet (miscEquips[0]) e o de luz pelo slot de luz ([1]):
//     o jogo poe o buff sozinho; tirar do slot some com o pet;
//   lacaio: nasce, tem o buff e o contador sob o buff, fere um slime ao
//     encostar (MinionContactDamage); usar de novo com 1 vaga troca o lacaio;
//     sem lacaio, o buff sai (o DelBuff dentro do UpdatePlayer);
//   sentinela: nasce, atira o ExampleSentryShot no slime; usar de novo com 1
//     vaga troca a sentinela; a do chao pousa e fica (nao afunda).
// Loga "summons <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('summons ' + label + ': ok');
        else { fails++; bl.log('summons ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('summons ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}
let items, projs, buffs;

const me = () => Main.player[Main.myPlayer];
const hasBuff = (p, t) => p['int FindBuffIndex(int type)'](t) >= 0;
const buffTimeOf = (p, t) => { const i = p['int FindBuffIndex(int type)'](t); return i >= 0 ? p.buffTime[i] : -1; };
function owned(type) {
    const out = [];
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.owner === Main.myPlayer && pr.type === type) out.push(pr);
    }
    return out;
}
function killAll(type) {
    for (const pr of owned(type)) pr.active = false;
}
function setItem(item, type) {
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
}
const distance = (a, b) => Math.hypot(a.X - b.X, a.Y - b.Y);

let forceUse = false;
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

let savedSlot0 = null, savedSelected = 0, slime = null;
function hold(type) {
    const p = me();
    setItem(p.inventory[0], type);
    p.selectedItemState.selected = 0;
    p.statMana = p.statManaMax2;
}
function spawnSlime() {
    const p = me();
    const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 6 * 16,
                       Math.floor(p.position.Y), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
    const npc = Main.npc[idx];
    npc.lifeMax = 50000;
    npc.life = 50000;
    npc.damage = 0;
    return npc;
}

// Cada passo: [quadros, fn(quadro)]. O fn roda em todo quadro do passo.
const steps = [];
function step(frames, fn) { steps.push([frames, fn]); }

step(40, (f) => {
    if (f === 0) {
        const p = me();
        savedSlot0 = p.inventory[0].type;
        savedSelected = p.selectedItemState.selected;
        hold(items.ExamplePetItem);
        forceUse = true;
    }
    if (f === 10) forceUse = false;
});
step(60, (f) => {
    if (f !== 59) return;
    const p = me();
    check('pet pelo item: o buff entrou e o jogo o pos em 18000 (ref bool)', () =>
        buffTimeOf(p, buffs.ExamplePetBuff) > 17000 || 'tempo do buff ' + buffTimeOf(p, buffs.ExamplePetBuff));
    check('pet pelo item: um pet, perto do jogador', () => {
        const pets = owned(projs.ExamplePetProjectile);
        if (pets.length !== 1) return pets.length + ' pets';
        return distance(pets[0].Center, p.Center) < 600 || 'longe: ' + Math.round(distance(pets[0].Center, p.Center));
    });
    check('pet pelo item: usa a IA do Zephyr Fish (aiStyle do pet)', () => {
        const pr = owned(projs.ExamplePetProjectile)[0];
        return (pr && pr.aiStyle === 26) || 'aiStyle ' + (pr && pr.aiStyle);
    });
    p['void DelBuff(int b)'](p['int FindBuffIndex(int type)'](buffs.ExamplePetBuff));
});
step(30, (f) => {
    if (f !== 29) return;
    check('pet pelo item: sem o buff, o pet some', () => owned(projs.ExamplePetProjectile).length === 0 || 'ainda ha pet');
    setItem(me().miscEquips[0], items.ExamplePetItem);
});
step(60, (f) => {
    if (f !== 59) return;
    const p = me();
    check('slot de pet: o jogo poe o buff e o pet', () =>
        (hasBuff(p, buffs.ExamplePetBuff) && owned(projs.ExamplePetProjectile).length === 1) ||
        `buff ${hasBuff(p, buffs.ExamplePetBuff)}, pets ${owned(projs.ExamplePetProjectile).length}`);
    setItem(p.miscEquips[0], 0);
    const i = p['int FindBuffIndex(int type)'](buffs.ExamplePetBuff);
    if (i >= 0) p['void DelBuff(int b)'](i);
    setItem(p.miscEquips[1], items.ExampleLightPetItem);
});
let alphas = [];
step(120, (f) => {
    const light = owned(projs.ExampleLightPetProjectile)[0];
    if (light) alphas.push(light.alpha);
    if (f !== 119) return;
    const p = me();
    check('pet: tirado do slot, nao volta', () =>
        (!hasBuff(p, buffs.ExamplePetBuff) && owned(projs.ExamplePetProjectile).length === 0) || 'o pet voltou');
    check('slot de luz: o jogo poe o buff e o pet de luz', () =>
        (hasBuff(p, buffs.ExampleLightPetBuff) && owned(projs.ExampleLightPetProjectile).length === 1) ||
        `buff ${hasBuff(p, buffs.ExampleLightPetBuff)}, pets ${owned(projs.ExampleLightPetProjectile).length}`);
    check('pet de luz: aparece aos poucos (alpha de ~255 a 0)', () =>
        (alphas.length > 30 && alphas.some((a) => a > 150) && alphas.indexOf(0, alphas.findIndex((a) => a > 150)) > 0) || 'alphas ' + alphas.slice(0, 40).join(','));
    check('pet de luz: e pet de luz para o jogo', () =>
        (Main.lightPet[buffs.ExampleLightPetBuff] && Terraria.ID.ProjectileID.Sets.LightPet[projs.ExampleLightPetProjectile]) || 'tabelas');
    setItem(p.miscEquips[1], 0);
    const i = p['int FindBuffIndex(int type)'](buffs.ExampleLightPetBuff);
    if (i >= 0) p['void DelBuff(int b)'](i);
    killAll(projs.ExampleLightPetProjectile);
    slime = spawnSlime();
    hold(items.ExampleMinionItem);
    forceUse = true;
});
let slimeLife = 0;
step(40, (f) => {
    if (f === 10) forceUse = false;
    if (f !== 39) return;
    const p = me();
    check('lacaio: nasceu, com o buff', () =>
        (owned(projs.ExampleMinion).length === 1 && hasBuff(p, buffs.ExampleMinionBuff)) ||
        `lacaios ${owned(projs.ExampleMinion).length}, buff ${hasBuff(p, buffs.ExampleMinionBuff)}`);
    check('lacaio: dano de invocacao guardado (originalDamage)', () => {
        const m = owned(projs.ExampleMinion)[0];
        return (m && m.originalDamage === 30 && m.minion) || `originalDamage ${m && m.originalDamage}`;
    });
    check('lacaio: contador sob o buff (BuffTextHandlers)', () =>
        Terraria.ID.BuffID.Sets.BuffTextHandlers.ContainsKey(buffs.ExampleMinionBuff) || 'sem contador');
    slimeLife = slime.life;
});
step(300, (f) => {
    const m = owned(projs.ExampleMinion)[0];
    if (m && slime.active && f % 30 === 0) {
        // Perto do slime: o teste e o dano ao encostar, nao a perseguicao.
        m.Center = Vector2.new(slime.Center.X, slime.Center.Y);
    }
    if (f !== 299) return;
    check('lacaio: fere o slime ao encostar (MinionContactDamage)', () =>
        (slime.active && slime.life < slimeLife) || `vida ${slime.life} de ${slimeLife}, ativo ${slime.active}`);
    hold(items.ExampleMinionItem);
    forceUse = true;
});
step(40, (f) => {
    if (f === 10) forceUse = false;
    if (f !== 39) return;
    check('lacaio: usar de novo com 1 vaga troca o lacaio (continua 1)', () =>
        owned(projs.ExampleMinion).length === 1 || owned(projs.ExampleMinion).length + ' lacaios');
    killAll(projs.ExampleMinion);
});
step(20, (f) => {
    if (f !== 19) return;
    check('lacaio: sem lacaio, o buff sai', () => !hasBuff(me(), buffs.ExampleMinionBuff) || 'o buff ficou');
    if (!slime.active) slime = spawnSlime();
    slimeLife = slime.life;
    hold(items.ExampleSentryItem);
    forceUse = true;
});
let shots = 0;
step(240, (f) => {
    if (f === 10) forceUse = false;
    if (owned(projs.ExampleSentryShot).length > 0) shots++;
    if (f === 60) {
        const s = owned(projs.ExampleSentry)[0];
        if (s && slime.active) slime.Center = Vector2.new(s.Center.X + 5 * 16, s.Center.Y);
    }
    if (f !== 239) return;
    check('sentinela: nasceu, e e sentinela', () => {
        const s = owned(projs.ExampleSentry);
        return (s.length === 1 && s[0].sentry) || s.length + ' sentinelas';
    });
    check('sentinela: atirou no slime', () => shots > 0 || 'nenhum tiro');
    hold(items.ExampleSentryItem);
    forceUse = true;
});
step(40, (f) => {
    if (f === 10) forceUse = false;
    if (f !== 39) return;
    check('sentinela: usar de novo com 1 vaga troca a sentinela (continua 1)', () =>
        owned(projs.ExampleSentry).length === 1 || owned(projs.ExampleSentry).length + ' sentinelas');
    killAll(projs.ExampleSentry);
    killAll(projs.ExampleSentryShot);
    if (slime.active) slime.active = false;
    const p = me();
    setItem(p.inventory[0], savedSlot0);
    p.selectedItemState.selected = savedSelected;
    const c = p.Center;
    // ai2 = 1: a do chao (com gravidade), como o item faz com o jogador virado
    // para a esquerda.
    newProj(Terraria.DataStructures.EntitySource_DebugCommand.new(), Vector2.new(c.X + 3 * 16, c.Y - 40), Vector2.Zero,
            projs.ExampleSentry, 50, 3, Main.myPlayer, 0, 0, 1, null);
});
let restY = 0;
step(180, (f) => {
    const s = owned(projs.ExampleSentry)[0];
    if (f === 120 && s) restY = s.position.Y;
    if (f !== 179) return;
    check('sentinela: pousa no chao e fica (OnTileCollide false nao afunda)', () =>
        (s && s.velocity.Y === 0 && Math.abs(s.position.Y - restY) < 0.5) ||
        (s ? `y ${restY.toFixed(1)} -> ${s.position.Y.toFixed(1)}, vy ${s.velocity.Y.toFixed(2)}` : 'sumiu'));
    killAll(projs.ExampleSentry);
});

let frames = 0, current = -1, stepFrame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;
    if (current < 0) {
        check('preparo', () => {
            items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
            projs = byName(bl.projectiles.vanillaCount, bl.projectiles.isModProjectile, ModProjectile.getModProjectile);
            buffs = byName(bl.buffs.vanillaCount, bl.buffs.isModBuff, ModBuff.getModBuff);
            const need = [items.ExamplePetItem, items.ExampleLightPetItem, items.ExampleMinionItem, items.ExampleSentryItem,
                          projs.ExampleMinion, projs.ExampleSentry, buffs.ExamplePetBuff, buffs.ExampleMinionBuff];
            return need.every((t) => t > 0) || 'Example Mod sem o conteudo da etapa 5: ' + need.join(',');
        });
        if (fails > 0) {
            done = true;
            bl.log('summons FIM: ' + fails + ' falha(s)');
            return;
        }
        current = 0;
        stepFrame = 0;
    }
    try {
        steps[current][1](stepFrame);
    } catch (e) {
        fails++;
        bl.log('summons passo ' + current + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
    if (++stepFrame >= steps[current][0]) {
        stepFrame = 0;
        if (++current >= steps.length) {
            done = true;
            forceUse = false;
            bl.log('summons FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        }
    }
});
bl.log('summons: carregado');
