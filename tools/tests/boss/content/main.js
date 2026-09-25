// O chefe do Example Mod nasce: a noite, o item de invocacao usado como o dedo
// usaria (controlUseItem); o chefe aparece, anda na direcao do jogador e
// anima. So isso: o resto do chefe e testado a mao. Loga "boss ...".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('boss ' + label + ': ok');
        else { fails++; bl.log('boss ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('boss ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}

let forceUse = false, bossType = -1, boss = null, firstPos = null, frames0 = new Set();
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

function findBoss() {
    for (let i = 0; i < Main.npc.length - 1; i++) {
        if (Main.npc[i].active && Main.npc[i].type === bossType) return Main.npc[i];
    }
    return null;
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) {
        check('preparo', () => {
            const items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
            bossType = byName(bl.npcs.vanillaCount, bl.npcs.isModNpc, ModNPC.getModNPC).ExampleBoss;
            Main.dayTime = false;
            Main.time = 0;
            self.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](items.ExampleBossSummonItem, null);
            self.selectedItemState.selected = 0;
            forceUse = true;
            return bossType > 0 || 'sem o ExampleBoss';
        });
    }
    if (frames === 80) forceUse = false;
    if (frames === 100) {
        boss = findBoss();
        check('o item de invocacao faz o chefe nascer', () => !!boss || 'nenhum chefe');
        if (boss) firstPos = Vector2.Clone(boss.Center);
    }
    if (boss && frames > 100 && frames < 300) frames0.add(boss.frame.Y);
    if (frames === 300) {
        done = true;
        check('o chefe anda (na direcao do jogador)', () =>
            (boss && boss.active && Vector2.Distance(boss.Center, firstPos) > 50) || 'parado');
        check('o chefe anima (varios quadros)', () => frames0.size >= 2 || 'quadros ' + [...frames0].join(','));
        bl.log('boss FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('boss: carregado');
