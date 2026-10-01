// Prefixos de mod (ModPrefix/PrefixLoader) com os do Example Mod. Duas rodadas,
// cada uma num processo novo (tools/bench/run.sh reabre o jogo):
//   1a: status, nome, tooltip, rolagem, categorias do item de mod, acessório;
//       põe três itens com prefixo de mod no inventário e um num baú e salva;
//   2a: este mod registra um prefixo A MAIS (ShiftPrefix) antes do Example Mod
//       (uid menor carrega antes), então os números dos prefixos do Example Mod
//       andam um: o save tem de devolvê-los pelo nome. Confere e limpa.
// Precisa do Example Mod ligado. Loga "prefix <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;
const f = Math.fround;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('prefix ' + label + ': ok');
        else { fails++; bl.log('prefix ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('prefix ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const STATE = () => bl.path.join(bl.mod.dataDirectory, 'rodada.json');
function readState() {
    try {
        const text = bl.file.read(STATE());
        return text ? JSON.parse(text) : null;
    } catch (e) {
        return null;
    }
}
// Lido na carga, antes do Example Mod: decide se a rodada é a 2a.
const saved = readState();

// Custom: não rola sozinho; só existe para empurrar os números na 2a rodada.
export class ShiftPrefix extends ModPrefix {
    static Autoload = false;
}

const me = () => Main.player[Main.myPlayer];
const T = (name) => ModContent.ItemType(name);
const P = (name) => ModContent.PrefixType('examplemod/' + name);
const sample = (type) => Terraria.ID.ContentSamples.ItemsByType.get_Item(type);
const roundEven = (x) => {
    const r = Math.round(x);
    return Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
};
function newItem(type, prefix = 0) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (prefix) it['bool Prefix(int prefixWeWant)'](prefix);
    return it;
}
function setSlot(item, type, prefix = 0) {
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (prefix) item['bool Prefix(int prefixWeWant)'](prefix);
}
const has = (arr, id) => {
    if (!arr) return false;
    for (let i = 0; i < arr.length; i++) if (arr[i] === id) return true;
    return false;
};

// O que o jogo escreve no tooltip (a mesma chamada do MouseText).
function tooltipOf(item) {
    const lines = Main.keyString.cloneResized(30);
    const pre = Main.projHostile.cloneResized(30);
    const bad = Main.projHostile.cloneResized(30);
    for (let i = 0; i < 30; i++) { lines[i] = null; pre[i] = false; bad[i] = false; }
    lines[0] = item['string AffixName()']();
    const n = new Ref(1);
    Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour, ref int sharedLine)'](
        item, new Ref(-1), new Ref(-1), new Ref(-1), item.knockBack, n, lines, pre, bad, new Ref(-1), new Ref(Color.new(255, 255, 255, 255)), new Ref(-1));
    const out = [];
    for (let i = 0; i < n.value; i++) out.push((pre[i] ? '+' : ' ') + lines[i]);
    return out;
}

function round1Static() {
    const ex = P('ExamplePrefix'), derived = P('ExampleDerivedPrefix'), acc = P('ExampleAccessoryPrefix');
    const vanilla = bl.items.vanillaPrefixCount();

    check('números depois dos do jogo', () =>
        (ex >= vanilla && derived >= vanilla && acc >= vanilla && new Set([ex, derived, acc]).size === 3) ||
        `vanilla ${vanilla}, ${ex}/${derived}/${acc}`);
    check('Lang.prefix e ReducedNaturalChance crescidos', () =>
        (Terraria.Lang.prefix.length > Math.max(ex, derived, acc) &&
         Terraria.ID.PrefixID.Sets.ReducedNaturalChance.length > Math.max(ex, derived, acc)) ||
        `${Terraria.Lang.prefix.length}/${Terraria.ID.PrefixID.Sets.ReducedNaturalChance.length}`);
    check('nome no Lang.prefix', () => {
        const name = Terraria.Lang.prefix[ex].Value;
        return (name === 'Example Prefix' || name === 'de Exemplo') || name;
    });

    const base = sample(ItemID.NightsEdge);
    const sword = newItem(ItemID.NightsEdge, ex);
    check('Prefix(ExamplePrefix) aplica +20% de dano', () =>
        (sword.prefix === ex && sword.damage === roundEven(base.damage * f(1.2))) ||
        `prefix ${sword.prefix}, dano ${sword.damage} (base ${base.damage})`);
    check('preço e raridade como o jogo calcula', () => {
        // O ModifyValue do exemplo multiplica em double (JS), como o loader guarda.
        const v = f(f(1.2) * (1 + 0.05));
        const expected = Math.trunc(base.value * f(v * v));
        // A conta final do jogo (valor x num2) sai em float: 1 de diferença é arredondamento.
        return (Math.abs(sword.value - expected) <= 1 && sword.rare === base.rare + 2) ||
            `valor ${sword.value} (esperado ${expected}), raridade ${sword.rare} (base ${base.rare})`;
    });
    check('nome com o prefixo (AffixName)', () => {
        const n = sword['string AffixName()']();
        return (n.indexOf(Terraria.Lang.prefix[ex].Value) >= 0 && n !== base.Name) || n;
    });
    const sword2 = newItem(ItemID.NightsEdge, derived);
    check('ExampleDerivedPrefix herda e dobra (+40%)', () =>
        sword2.damage === roundEven(base.damage * f(1.4)) || `dano ${sword2.damage}`);

    // As linhas do GetTooltipLines entram no desenho do tooltip (TooltipLoader,
    // conferido pelo tests/tooltipdraw); o GetLinesInfo é só o do jogo.
    check('tooltip: +% de dano do jogo e as linhas do prefixo', () => {
        const lines = tooltipOf(sword);
        const extra = ModContent.GetModPrefix(ex).GetTooltipLines(sword).map((l) => (l.IsModifier ? '+' : ' ') + l.Text);
        const joined = lines.concat(extra).join(' | ');
        const power = extra.some((l) => l[0] === '+' && /Power|Poder/.test(l));
        const more = extra.some((l) => l[0] === '+' && /More Power|Mais Poder/.test(l));
        const dmg = lines.some((l) => l[0] === '+' && /20%/.test(l));
        return (power && more && dmg) || joined;
    });

    // O item de mod: melee e magic (os da classe dele), sem os de longo alcance.
    const multi = newItem(T('ExampleMultiplePrefixCategoryWeapon'));
    check('item de mod: categorias do MeleePrefix/MagicPrefix', () => {
        const list = multi['int[] GetRollablePrefixes()']();
        const got = { legendary: has(list, 81), mythical: has(list, 83), unreal: has(list, 82), example: has(list, ex),
                      accessory: has(list, acc) };
        return (got.legendary && got.mythical && !got.unreal && got.example && !got.accessory) || JSON.stringify(got);
    });
    check('item de mod sem categoria nova: a espada pega os de espada', () => {
        const list = newItem(T('ExampleMeleeWeapon'))['int[] GetRollablePrefixes()']();
        return (has(list, 81) && has(list, ex)) || String(list && list.length);
    });
    check('munição de mod não ganha prefixo', () => {
        const bullet = newItem(T('ExampleBullet'));
        return !bullet['bool CanHavePrefixes()']() || 'CanHavePrefixes true';
    });
    check('acessório: só o prefixo de acessório', () => {
        const list = newItem(ItemID.CloudinaBottle)['int[] GetRollablePrefixes()']();
        return (has(list, acc) && has(list, 65) && !has(list, ex)) || String(list && list.length);
    });

    // Rolagem com peso: 5 para cada um dos dois do Example Mod.
    check('reforja rola os prefixos de mod (RollChance)', () => {
        let mine = 0, total = 400;
        for (let i = 0; i < total; i++) {
            const it = newItem(ItemID.NightsEdge);
            it['bool Prefix(int prefixWeWant)'](-2);
            if (it.prefix === ex || it.prefix === derived) mine++;
        }
        const rate = mine / total;
        return (rate > 0.05 && rate < 0.6) || `${mine}/${total}`;
    });
    check('baú (ChestItem) também rola os de mod', () => {
        // O ChestItem rola pela amostra: a mesma cadeia de hooks.
        let mine = 0;
        for (let i = 0; i < 400; i++) {
            const it = newItem(ItemID.NightsEdge);
            it['bool Prefix(int prefixWeWant)'](-1);
            if (it.prefix === ex || it.prefix === derived) mine++;
        }
        return mine > 0 || '0 de 400';
    });
}

// Acessório: +4 de defesa com o prefixo, por quadro.
let defenseWith = -1, defenseWithout = -1, accSaved = null;
function accessoryOn() {
    const slot = me().armor[3];
    accSaved = { type: slot.type, prefix: slot.prefix };
    setSlot(slot, ItemID.CloudinaBottle, P('ExampleAccessoryPrefix'));
}
function accessoryMid() {
    defenseWith = me().statDefense;
    const slot = me().armor[3];
    setSlot(slot, ItemID.CloudinaBottle);
}
function accessoryOff() {
    defenseWithout = me().statDefense;
    check('acessório com o prefixo: +4 de defesa (ApplyAccessoryEffects)', () =>
        defenseWith - defenseWithout === 4 || `${defenseWith} com, ${defenseWithout} sem`);
    setSlot(me().armor[3], accSaved.type, accSaved.prefix);
}

// Os itens que o save leva: [slot, tipo, prefixo].
function saveRound() {
    const p = me();
    const empty = [];
    for (let i = 10; i < 50 && empty.length < 3; i++) if (p.inventory[i].type === 0) empty.push(i);
    if (empty.length < 3) { check('achar 3 lugares vazios no inventário', () => 'só ' + empty.length); return; }

    const plan = [
        [empty[0], ItemID.NightsEdge, 'ExamplePrefix'],
        [empty[1], T('ExampleMultiplePrefixCategoryWeapon'), 'ExampleDerivedPrefix'],
        [empty[2], ItemID.CloudinaBottle, 'ExampleAccessoryPrefix'],
    ];
    for (const [slot, type, name] of plan) setSlot(p.inventory[slot], type, P(name));
    check('inventário com os prefixos de mod', () =>
        plan.every(([slot, , name]) => p.inventory[slot].prefix === P(name)) ||
        plan.map(([slot]) => p.inventory[slot].prefix).join(','));

    // Um baú qualquer do mundo, numa casa vazia.
    let chestAt = null;
    for (let c = 0; c < Main.chest.length && !chestAt; c++) {
        const chest = Main.chest[c];
        if (!chest) continue;
        for (let k = 0; k < chest.item.length; k++) {
            if (chest.item[k].type === 0) { chestAt = { x: chest.x, y: chest.y, k }; break; }
        }
    }
    if (chestAt) {
        const chest = Main.chest[Terraria.Chest['int FindChest(int X, int Y)'](chestAt.x, chestAt.y)];
        const ci = chest.item[chestAt.k];
        ci.type = ItemID.NightsEdge;
        ci.stack = 1;
        ci.prefix = P('ExampleDerivedPrefix');
        check('baú com prefixo de mod', () => chest.item[chestAt.k].prefix === P('ExampleDerivedPrefix') ||
            'prefixo ' + chest.item[chestAt.k].prefix);
    } else {
        check('achar um baú no mundo', () => 'nenhum baú');
    }

    const ids = { ExamplePrefix: P('ExamplePrefix'), ExampleDerivedPrefix: P('ExampleDerivedPrefix'),
                  ExampleAccessoryPrefix: P('ExampleAccessoryPrefix') };
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](
        Main.ActivePlayerFileData, false, true);
    Terraria.IO.WorldFile['void SaveWorld(WorldFile.WorldSaveContext saveContext)'](0);

    check('o .bl guarda o prefixo pelo nome', () => {
        const text = bl.file.read(Main.ActivePlayerFileData.Path + '.bl') || '';
        return (text.indexOf('vanilla:' + ItemID.NightsEdge) >= 0 && text.indexOf('/ExamplePrefix') >= 0 &&
                text.indexOf('/ExampleDerivedPrefix') >= 0) || text.slice(0, 400);
    });

    bl.file.write(STATE(), JSON.stringify({ plan: plan.map(([slot, type, name]) => [slot, type, name]), chestAt, ids }));
    bl.log('prefix FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)') + ' (1a rodada); rode de novo');
}

function verifyRound() {
    const p = me();
    const { plan, chestAt, ids } = saved;

    check('os números andaram (ShiftPrefix carregou antes)', () =>
        P('ExamplePrefix') === ids.ExamplePrefix + 1 || `${ids.ExamplePrefix} -> ${P('ExamplePrefix')}`);
    for (const [slot, type, name] of plan) {
        check(`inventário ${slot}: ${name} de volta pelo nome`, () => {
            const it = p.inventory[slot];
            const fresh = newItem(type, P(name));
            return (it.type === type && it.prefix === P(name) && it.damage === fresh.damage && it.value === fresh.value) ||
                `tipo ${it.type}, prefixo ${it.prefix} (quer ${P(name)}), dano ${it.damage}/${fresh.damage}`;
        });
    }
    if (chestAt) {
        const chest = Main.chest[Terraria.Chest['int FindChest(int X, int Y)'](chestAt.x, chestAt.y)];
        check('baú: prefixo de volta pelo nome', () =>
            (chest.item[chestAt.k].type === ItemID.NightsEdge && chest.item[chestAt.k].prefix === P('ExampleDerivedPrefix')) ||
            `tipo ${chest.item[chestAt.k].type}, prefixo ${chest.item[chestAt.k].prefix} (quer ${P('ExampleDerivedPrefix')})`);
        const ci = chest.item[chestAt.k];
        ci.type = 0;
        ci.stack = 0;
        ci.prefix = 0;
    }

    for (const [slot] of plan) setSlot(p.inventory[slot], 0);
    Terraria.Player['void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)'](
        Main.ActivePlayerFileData, false, true);
    Terraria.IO.WorldFile['void SaveWorld(WorldFile.WorldSaveContext saveContext)'](0);
    bl.file.delete(STATE());
    bl.log('prefix FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)') + ' (2a rodada)');
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (!self.dead) self.statLife = self.statLifeMax2;
    ++frames;
    if (saved) {
        if (frames === 90) { verifyRound(); done = true; }
        return;
    }
    if (frames === 60) round1Static();
    if (frames === 70) accessoryOn();
    if (frames === 80) accessoryMid();
    if (frames === 90) accessoryOff();
    if (frames === 100) { saveRound(); done = true; }
});
bl.log('prefix: carregado (' + (saved ? '2a' : '1a') + ' rodada)');

// A classe do mod, obrigatória no arquivo de entrada.
export default class PrefixTestMod extends Mod {
    Load() {
        if (saved) ModPrefix.register(ShiftPrefix);
    }
}
