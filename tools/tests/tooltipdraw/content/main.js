// O tooltip de mod (TooltipLoader) e a raridade de mod (RarityLoader) de
// verdade na tela: por uns quadros depois de entrar no mundo, o hook do
// DrawPendingMouseText põe um item de teste no HoverItem e pede o MouseText,
// como o dedo em cima do item. O item grava o que os hooks dele recebem, e no
// fim os casos são conferidos. Loga "tooltipdraw <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tooltipdraw ' + label + ': ok');
        else { fails++; bl.log('tooltipdraw ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tooltipdraw ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class TestRarity extends ModRarity {
    get RarityColor() { return Color.new(10, 200, 30); }
    GetPrefixedRarity(offset, valueMult) { return offset > 0 ? ModContent.RarityType(TestRarityUp) : this.Type; }
}

export class TestRarityUp extends ModRarity {
    get RarityColor() { return Color.new(200, 10, 30); }
    GetPrefixedRarity(offset, valueMult) { return offset < 0 ? ModContent.RarityType(TestRarity) : this.Type; }
}

// +20% de dano, e uma linha a mais no tooltip.
export class TestPrefix extends ModPrefix {
    RollChance(item) { return 0; }
    SetStats(stats) { stats.damage = 1.2; }
    GetTooltipLines(item) {
        const line = new TooltipLine(this.Mod, 'PrefixTest', 'linha do prefixo de teste');
        line.IsModifier = true;
        return [line];
    }
}

const seen = { modify: null, pre: null, lines: new Map(), post: 0, postAll: null, frames: 0 };

export class TestTooltipItem extends ModItem {
    // Uma pasta qualquer do mod, como o `override string Texture =>` do tModLoader.
    get Texture() { return 'Arte/Espadas/TestTooltipItem'; }

    constructor() {
        super();
        this.DisplayName = 'Espada do tooltip';
        this.Tooltip = 'Linha A\nLinha B';
    }

    SetDefaults(item) {
        item.width = item.height = 20;
        item.melee = true;
        item.damage = 20;
        item.knockBack = 5;
        item.useStyle = 1;
        item.useTime = item.useAnimation = 20;
        item.rare = ModContent.RarityType(TestRarity);
    }

    ModifyTooltips(item, tooltips) {
        seen.modify = tooltips.map((l) => l.Name);
        tooltips.splice(1, 0, new TooltipLine(this.Mod, 'Extra', 'linha extra'));
        const kb = tooltips.find((l) => l.Name === 'Knockback');
        if (kb) kb.Hide();
        const a = tooltips.find((l) => l.Name === 'Tooltip0');
        if (a) a.OverrideColor = Color.new(255, 215, 90);
    }

    PreDrawTooltip(item, lines, x, y) {
        seen.pre = { count: lines.length, x: x.value, y: y.value };
        return true;
    }

    PreDrawTooltipLine(item, line, yOffset) {
        seen.lines.set(line.Name, { X: line.X, Y: line.Y, R: line.Color.R, G: line.Color.G, B: line.Color.B, A: line.Color.A,
                                    index: line.Index, modifier: line.IsModifier });
        if (line.Name === 'Extra') yOffset.value = 6;
        return line.Name !== 'Tooltip1';
    }

    PostDrawTooltipLine(item, line) { seen.post++; }

    PostDrawTooltip(item, lines) { seen.postAll = lines.map((l) => l.Name); }

    // Uma receita, para o popup do guia de criação mostrar o item.
    AddRecipes() {
        this.CreateRecipe().AddIngredient(Terraria.ID.ItemID.DirtBlock).Register();
    }
}

function newItem(type, prefix = 0) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    if (prefix) it['bool Prefix(int prefixWeWant)'](prefix);
    return it;
}

function rarityChecks() {
    const low = ModContent.RarityType(TestRarity), up = ModContent.RarityType(TestRarityUp);
    const item = ModContent.ItemType(TestTooltipItem);

    check('textura: get Texture() numa pasta própria (Arte/Espadas)', () => {
        const t = Terraria.GameContent.TextureAssets.Item[item].Value;
        return (t.Width === 26 && t.Height === 26) || `${t.Width} x ${t.Height}`;
    });
    check('raridade: tipos depois das 12 do jogo', () => (low >= 12 && up === low + 1) || `${low}/${up}`);
    check('raridade: ModContent.GetModRarity', () => ModContent.GetModRarity(low) instanceof TestRarity);
    check('raridade: GetPopupRarityColor', () => {
        const c = Terraria.Item['Color GetPopupRarityColor(int itemRarity)'](low);
        return (c.R === 10 && c.G === 200 && c.B === 30) || `${c.R},${c.G},${c.B}`;
    });
    check('raridade: ItemRarity.GetColor (tabela)', () => {
        const c = Terraria.GameContent.UI.ItemRarity.GetColor(up);
        return (c.R === 200 && c.G === 10 && c.B === 30) || `${c.R},${c.G},${c.B}`;
    });
    check('raridade: prefixo bom sobe (GetPrefixedRarity)', () => {
        const it = newItem(item, ModContent.PrefixType(TestPrefix));
        return it.rare === up || `rare ${it.rare}, prefixo ${it.prefix}`;
    });
    check('raridade: prefixo ruim fica na de mod (o jogo daria 10)', () => {
        const it = newItem(item, 39);   // Quebrado: dano 0,7, repulsão 0,8
        return (it.prefix === 39 && it.rare === low) || `rare ${it.rare}, prefixo ${it.prefix}`;
    });
}

// O popup do guia (GUICraftGuidePopup.UpdateText) com a receita do item de teste:
// as linhas vêm do ModifyTooltips, como no tooltip.
function guideChecks() {
    check('popup do guia: linhas do ModifyTooltips', () => {
        const type = ModContent.ItemType(TestTooltipItem);
        let index = -1;
        for (let r = 0; r < Terraria.Recipe.numRecipes; r++) {
            if (Main.recipe[r].createItem.type === type) { index = r; break; }
        }
        if (index < 0) return 'sem a receita';
        const pop = GUIInstance.Active.GUICraftGuidePopup;
        pop.availableGuideRecipe[0] = index;
        pop.numAvailableGuideRecipes = 1;
        pop.focusGuideRecipe = 0;
        pop['void UpdateText()']();
        const text = String(pop.displayString);
        bl.log('tooltipdraw: popup do guia "' + text.replace(/\n/g, ' | ') + '"');
        return text.includes('linha extra') || text;
    });
}

function drawChecks() {
    check('tooltip: desenhado pelo TooltipLoader', () => (seen.pre !== null && seen.postAll !== null) || `pre ${!!seen.pre}, post ${!!seen.postAll}`);
    check('tooltip: nomes do tModLoader no ModifyTooltips', () => {
        const want = ['ItemName', 'Damage', 'CritChance', 'Speed', 'Knockback', 'Tooltip0', 'Tooltip1', 'PrefixDamage', 'PrefixTest'];
        const names = seen.modify || [];
        let at = -1;
        for (const w of want) {
            const i = names.indexOf(w);
            if (i <= at) return 'ordem: ' + names.join(',');
            at = i;
        }
        return true;
    });
    check('tooltip: linha nova no índice 1 e a escondida some', () => {
        const names = seen.postAll || [];
        return (names[1] === 'Extra' && !names.includes('Knockback')) || names.join(',');
    });
    check('tooltip: PreDrawTooltip recebe as linhas e x, y', () =>
        (seen.pre.count === seen.postAll.length && Number.isFinite(seen.pre.x) && Number.isFinite(seen.pre.y)) || JSON.stringify(seen.pre));
    check('tooltip: PostDrawTooltipLine em toda linha (também a não desenhada)', () =>
        seen.post % seen.postAll.length === 0 || `${seen.post} / ${seen.postAll.length}`);
    // O prefixo bom subiu o item para a TestRarityUp (vermelha).
    check('tooltip: cor do nome = raridade de mod', () => {
        const c = seen.lines.get('ItemName');
        return (c && c.R > c.G * 5 && c.R > c.B * 3) || JSON.stringify(c);
    });
    check('tooltip: OverrideColor', () => {
        const c = seen.lines.get('Tooltip0');
        return (c && c.R > c.B * 2 && c.G > c.B * 2) || JSON.stringify(c);
    });
    check('tooltip: linha do prefixo de mod é modificador (verde)', () => {
        const c = seen.lines.get('PrefixTest');
        return (c && c.modifier && c.G > c.R && c.G > c.B) || JSON.stringify(c);
    });
    check('tooltip: yOffset soma depois da linha', () => {
        const name = seen.lines.get('ItemName'), extra = seen.lines.get('Extra'), damage = seen.lines.get('Damage');
        const before = extra.Y - name.Y, after = damage.Y - extra.Y;
        return after - before === 6 || `antes ${before}, depois ${after}`;
    });
}

let sample = null, frames = 0, done = false;

Terraria.Main['void DrawPendingMouseText(bool worldMouse)'].hook((original, worldMouse) => {
    if (sample && !done && !worldMouse) {
        Main.HoverItem = sample['Item Clone()']();
        Main.inventoryTooltipTime = 30;
        Main.instance['void MouseText(string cursorText, int rare, byte diff, int hackedMouseX, int hackedMouseY, int hackedScreenWidth, int hackedScreenHeight, int pushWidthX)'](
            sample.Name, sample.rare, 0, -1, -1, -1, -1, 0);
        seen.frames++;
    }
    return original();
});

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 120) {
        rarityChecks();
        guideChecks();
        sample = newItem(ModContent.ItemType(TestTooltipItem), ModContent.PrefixType(TestPrefix));
        bl.log('tooltipdraw: item de teste no HoverItem (rare ' + sample.rare + ', prefixo ' + sample.prefix + ')');
    }
    if (frames === 240) {
        bl.log('tooltipdraw: quadros com o MouseText pedido: ' + seen.frames);
        drawChecks();
        bl.log('tooltipdraw FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
    // Com o Example Mod: o tooltip do ExampleTooltipItem (a linha em onda), para uma captura.
    if (frames === 300) {
        const example = ModContent.ItemType('ExampleTooltipItem');
        if (example > 0) {
            sample = newItem(example);
            bl.log('tooltipdraw: ExampleTooltipItem no HoverItem');
        }
    }
    // Mais uns 15 s na tela, para uma captura (adb exec-out screencap).
    if (frames === 1200) done = true;
});

export default class TestTooltipDraw extends Mod {}
