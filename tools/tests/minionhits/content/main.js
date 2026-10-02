// Acertos do ExampleMinion por quadro: encostado num slime de vida alta, o
// lacaio (penetrate -1) deve acertar no máximo uma vez a cada ~10 quadros
// (immune[dono] = 10 do jogo). Conta por quadro as chamadas de Damage, de
// StatusNPC e de StrikeNPC, a vida perdida e o immune[0] do slime.
// Loga "minionhits <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('minionhits ' + label + ': ok');
        else { fails++; bl.log('minionhits ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('minionhits ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const me = () => Main.player[Main.myPlayer];
let minionType = -1, itemType = -1;

function owned(type) {
    const out = [];
    for (let i = 0; i < 1000; i++) {
        const pr = Main.projectile[i];
        if (pr.active && pr.owner === Main.myPlayer && pr.type === type) out.push(pr);
    }
    return out;
}

let forceUse = false;
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

// Contadores do quadro atual.
let frame = { damage: 0, status: 0, strike: 0, updates: 0 };
Terraria.Projectile['void Damage()'].hook((original, p) => {
    if (p.type === minionType) frame.damage++;
    return original(p);
});
Terraria.Projectile['void StatusNPC(int i)'].hook((original, p, i) => {
    if (p.type === minionType) frame.status++;
    return original(p, i);
});
Terraria.Projectile['void Update(int i)'].hook((original, p, i) => {
    if (p.type === minionType) frame.updates++;
    return original(p, i);
});

let slime = null, slimeLife = 0, savedSlot0 = 0;
const rows = [];

let frames = 0, started = -1, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;

    const p = me();
    if (started < 0) {
        started = frames;
        for (let t = bl.projectiles.vanillaCount; bl.projectiles.isModProjectile(t); t++) {
            const m = ModProjectile.getModProjectile(t);
            if (m && m.constructor.name === 'ExampleMinion') minionType = t;
        }
        for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
            const m = ModItem.getModItem(t);
            if (m && m.constructor.name === 'ExampleMinionItem') itemType = t;
        }
        check('preparo', () => (minionType > 0 && itemType > 0) || `tipos ${minionType} ${itemType}`);
        if (fails) { done = true; bl.log('minionhits FIM: ' + fails + ' falha(s)'); return; }

        Main.dayTime = true;
        Main.time = 20000;
        savedSlot0 = p.inventory[0].type;
        p.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](itemType, null);
        p.selectedItemState.selected = 0;
        p.statMana = p.statManaMax2;
        const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.Center.X) + 6 * 16,
                           Math.floor(p.position.Y), Terraria.ID.NPCID.BlueSlime, 0, 0, 0, 0, 0, Main.myPlayer);
        slime = Main.npc[idx];
        slime.lifeMax = slime.life = 50000;
        slime.damage = 0;
        slimeLife = slime.life;
        forceUse = true;
        return;
    }

    const f = frames - started;
    if (f === 10) forceUse = false;
    p.statLife = p.statLifeMax2;
    p.fallStart = Math.floor(p.position.Y / 16);

    const m = owned(minionType)[0];
    if (f >= 40 && m && slime.active) {
        // Em cima do slime todo quadro: o caso de quem mais acerta.
        m.Center = Vector2.new(slime.Center.X, slime.Center.Y);
        m.velocity = Vector2.new(0, 0);
    }
    if (f >= 40) {
        rows.push({ f, ...frame, lost: slimeLife - slime.life, immune: slime.immune[Main.myPlayer] });
    }
    slimeLife = slime.life;
    frame = { damage: 0, status: 0, strike: 0, updates: 0 };

    if (f < 220) return;

    const hitRows = rows.filter((r) => r.status > 0);
    bl.log('minionhits diag: lacaios ' + owned(minionType).length + ', extraUpdates ' + (m ? m.extraUpdates : '?') +
           ', usesLocalNPCImmunity ' + (m ? m.usesLocalNPCImmunity : '?') + ', penetrate ' + (m ? m.penetrate : '?'));
    bl.log('minionhits diag: quadros com acerto ' + hitRows.map((r) => `f${r.f}:upd${r.updates}/dmg${r.damage}/hit${r.status}/-${r.lost}/imm${r.immune}`).join(' '));
    bl.log('minionhits diag: amostra ' + rows.slice(0, 25).map((r) => `f${r.f}:upd${r.updates}/dmg${r.damage}/hit${r.status}/imm${r.immune}`).join(' '));

    check('um Update e um Damage do lacaio por quadro', () => {
        const bad = rows.filter((r) => r.updates > 1 || r.damage > 1);
        return bad.length === 0 || bad.length + ' quadros, ex. ' + JSON.stringify(bad[0]);
    });
    check('no máximo um acerto por quadro', () => {
        const bad = rows.filter((r) => r.status > 1);
        return bad.length === 0 || bad.length + ' quadros, ex. ' + JSON.stringify(bad[0]);
    });
    check('imunidade entre acertos (>= 8 quadros)', () => {
        const gaps = [];
        for (let k = 1; k < hitRows.length; k++) gaps.push(hitRows[k].f - hitRows[k - 1].f);
        return (hitRows.length >= 3 && gaps.every((g) => g >= 8)) || 'acertos ' + hitRows.length + ', intervalos ' + gaps.join(',');
    });

    for (const pr of owned(minionType)) pr.active = false;
    if (slime.active) slime.active = false;
    p.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](savedSlot0, null);
    done = true;
    bl.log('minionhits FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('minionhits: carregado');

export default class TestMinionHits extends Mod {}
