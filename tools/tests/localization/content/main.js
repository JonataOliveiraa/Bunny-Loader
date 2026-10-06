// ModLocalization: Translate devolve o texto (como no TL), chaves em qualquer
// profundidade, as chaves Mods.<id>.* no LanguageManager do jogo sem passo
// extra e a troca de idioma. Loga "localization <caso>: ok | FALHOU".
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('localization ' + label + ': ok');
        else { fails++; bl.log('localization ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('localization ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const Language = Terraria.Localization.Language;
const getText = (key) => Language['LocalizedText GetText(string key)'](key).Value;
const TEXTS = {
    'en-US': { hello: 'Hello', deep: 'Deep text', full: 'The Wand', relative: 'My Wand', buy: 'Buy: ' },
    'pt-BR': { hello: 'Olá', deep: 'Texto fundo', full: 'A Varinha', relative: 'Minha Varinha', buy: 'Comprar: ' },
};
const REFS = 'Mods.test-localization.Refs.';

export class LocalizationItem extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) { item.width = item.height = 16; item.maxStack = 1; }
    ModifyTooltipLines() { this.TooltipLines[0] += '!'; this.TooltipLines.push('End'); }
}

export class LocalizationNPC extends ModNPC {
    Texture = 'Box';
    HideFromModMenu = true;
    HideFromBestiary = true;
    SetStaticDefaults() { Terraria.Main.npcFrameCount[this.Type] = 1; }
    SetDefaults(npc) { npc.width = npc.height = 16; npc.lifeMax = 10; npc.aiStyle = -1; }
}

export class LocalizationBuff extends ModBuff {
    Texture = 'Box';
}

export class LocalizationPrefix extends ModPrefix {}

export class LocalizationLiteralItem extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    SetDefaults(item) { item.width = item.height = 16; item.maxStack = 1; }
}

export class LocalizationLiteralProjectile extends ModProjectile {
    Texture = 'Box';
    SetDefaults(projectile) { projectile.width = projectile.height = 16; projectile.timeLeft = 1; }
}

function checkRegisteredContent(culture) {
    const pt = culture === 'pt-BR';
    const shop = Language['string GetTextValue(string key)']('LegacyInterface.28');
    const expected = (pt ? 'Comprar: ' : 'Buy: ') + shop;
    const itemType = ModContent.ItemType(LocalizationItem);
    const npcType = ModContent.NPCType(LocalizationNPC);
    const buffType = ModContent.BuffType(LocalizationBuff);
    const values = [Terraria.Lang['LocalizedText GetItemName(int id)'](itemType).Value,
        Terraria.Lang['LocalizedText GetNPCName(int netID)'](npcType).Value,
        Terraria.Lang._buffNameCache[buffType].Value,
        Terraria.Lang._buffDescriptionCache[buffType].Value,
        Terraria.Lang.prefix[ModContent.PrefixType(LocalizationPrefix)].Value];
    if (values.some(value => value !== expected)) return JSON.stringify(values);
    const literalItem = Terraria.Lang['LocalizedText GetItemName(int id)'](ModContent.ItemType(LocalizationLiteralItem)).Value;
    const literalProjectile = Terraria.Lang['LocalizedText GetProjectileName(int type)'](ModContent.ProjectileType(LocalizationLiteralProjectile)).Value;
    if (literalItem !== 'LocalizationLiteralItem' || literalProjectile !== 'LocalizationLiteralProjectile') {
        return JSON.stringify([literalItem, literalProjectile]);
    }
    const tooltip = Terraria.Lang._itemTooltipCache[itemType];
    const want = [pt ? 'Primeira!' : 'First!', expected, pt ? 'Varinha' : 'Wand', 'End'];
    const lines = [];
    for (let i = 0; i < tooltip.Lines; i++) lines.push(tooltip['string GetLine(int line)'](i));
    return JSON.stringify(lines) === JSON.stringify(want) || JSON.stringify(lines);
}

// Nome de NPC e de item de mod (a plaquinha do Bestiário lê a chave
// NPCName.X), se o Example Mod estiver instalado.
function contentNames() {
    let npc = -1, item = -1;
    for (let t = 0; t < 2000 && npc < 0; t++) if (ModNPC.getModNPC(t)?.constructor.name === 'ExamplePerson') npc = t;
    for (let t = 0; t < 8000 && item < 0; t++) if (ModItem.getModItem(t)?.constructor.name === 'ExampleItem') item = t;
    if (npc < 0 || item < 0) return null;
    return [Terraria.Lang['LocalizedText GetNPCName(int netID)'](npc).Value, getText('NPCName.ExamplePerson'),
            Terraria.Lang['LocalizedText GetItemName(int id)'](item).Value];
}
const CONTENT_NAMES = { 'en-US': ['Person', 'Person', 'Example Item'], 'pt-BR': ['Pessoa', 'Pessoa', 'Exemplo de Item'] };
function checkContentNames(culture) {
    const got = contentNames();
    if (!got) return true;   // sem o Example Mod
    return JSON.stringify(got) === JSON.stringify(CONTENT_NAMES[culture]) || JSON.stringify(got);
}

// Os {$chave} resolvidos no texto do jogo e no Translate.
function checkReferences(culture) {
    const e = TEXTS[culture];
    const shop = Language['string GetTextValue(string key)']('LegacyInterface.28');
    const want = {
        Full: e.full, Relative: e.relative, Game: e.buy + shop, Chain: '[' + e.full + ']',
        Args: '{1} of {2}', Missing: 'Refs.Nothing', Loop: 'a' + REFS + 'Loop',
    };
    const bad = [];
    for (const [key, text] of Object.entries(want)) {
        const got = getText(REFS + key);
        if (got !== text) bad.push(key + '=' + JSON.stringify(got));
    }
    const translated = ModLocalization.Translate('Refs.Full');
    if (translated !== e.full) bad.push('Translate=' + JSON.stringify(translated));
    return bad.length === 0 || bad.join(' ');
}
const expected = () => TEXTS[ModLocalization.ActiveCultureName] || TEXTS['en-US'];

// Na carga: o jogo ainda não tem as chaves, mas o Translate já responde.
check('Translate na carga', () => {
    const e = expected();
    return ModLocalization.Translate('CustomText.Hello') === e.hello || ModLocalization.Translate('CustomText.Hello');
});

export default class TestLocalization extends Mod {
    PostSetupContent() {
        const culture = ModLocalization.ActiveCultureName;
        bl.log('localization idioma do jogo: ' + culture);
        check('conteúdo com referências no idioma inicial', () => checkRegisteredContent(culture));

        check('Translate devolve o texto', () => {
            const e = expected();
            const got = [ModLocalization.Translate('CustomText.Hello'), ModLocalization.Translate('Deep.Level1.Level2')];
            return (got[0] === e.hello && got[1] === e.deep) || JSON.stringify(got);
        });
        check('Translate sem texto devolve o caminho', () =>
            ModLocalization.Translate('Nada.Aqui') === 'Nada.Aqui' && ModLocalization.TryTranslate('Nada.Aqui') === '');
        check('cultura sem o texto cai no inglês', () =>
            ModLocalization.Translate('CustomText.OnlyEnglish') === 'Only in English');
        check('Language.GetText direto, sem Translate antes', () => {
            const e = expected();
            const got = [getText('Mods.test-localization.CustomText.Hello'), getText('Mods.test-localization.Deep.Level1.Level2')];
            return (got[0] === e.hello && got[1] === e.deep) || JSON.stringify(got);
        });
        check('Key, GetText, GetTextValue, Exists', () => {
            const key = ModLocalization.Key('CustomText.Hello');
            if (key !== 'Mods.test-localization.CustomText.Hello') return 'Key ' + key;
            if (ModLocalization.Key('Nada.Aqui') !== 'Nada.Aqui') return 'Key sem texto';
            if (ModLocalization.GetText('CustomText.Hello').Value !== expected().hello) return 'GetText';
            if (ModLocalization.GetTextValue(key) !== expected().hello) return 'GetTextValue da chave cheia';
            const shop = ModLocalization.GetTextValue('LegacyInterface.28');
            if (!shop || shop === 'LegacyInterface.28') return 'GetTextValue do jogo ' + shop;
            return (ModLocalization.Exists('CustomText.Hello') && !ModLocalization.Exists('Nada.Aqui')) || 'Exists';
        });
        ModLocalization.Register('TestLocalization.Extra', { 'en-US': 'Extra', 'pt-BR': 'Extra BR' });
        ModLocalization.Register('TestLocalization.Ref', '{$' + REFS + 'Name}!');

        check('{$chave}: inteira, relativa, do jogo, em cadeia, @n, sem chave, circular', () => checkReferences(culture));
        check('Register com {$chave}', () => {
            const got = getText('TestLocalization.Ref');
            return got === getText(REFS + 'Name') + '!' || got;
        });
        check('categoria do jogo (GetCategorySize, FindAll, RandomFromCategory)', () => {
            const category = 'Mods.test-localization.Dialogue.Person';
            const size = Language['int GetCategorySize(string key)'](category);
            const all = Language['LocalizedText[] FindAll(string categoryName)'](category);
            const values = [];
            for (let i = 0; i < all.length; i++) values.push(all[i].Value);
            const random = Language['LocalizedText RandomFromCategory(string categoryName, UnifiedRandom random)'](category, null).Value;
            return (size === 2 && values.sort().join() === 'one,two' && (random === 'one' || random === 'two')) ||
                JSON.stringify([size, values, random]);
        });
        check('variante (Chave$Variante)', () => {
            const out = new Ref();
            const found = Language['bool TryGetVariation(string key, string variant, out string value)'](REFS + 'Hi', 'Formal', out);
            return (found && out.value === 'Good day') || JSON.stringify([found, out.value]);
        });

        // A troca de idioma volta tudo para a chave e recarrega só o jogo.
        const other = culture === 'pt-BR' ? 'en-US' : 'pt-BR';
        const manager = Terraria.Localization.LanguageManager.Instance;
        // O mesmo LocalizedText recebe o texto novo (quem o guardou, como o
        // Lang.prefix, não fica com o velho).
        const held = Language['LocalizedText GetText(string key)'](REFS + 'Full');
        check('troca de idioma (' + other + ')', () => {
            manager['void SetLanguage(string cultureName)'](other);
            const e = TEXTS[other];
            const got = [getText('Mods.test-localization.CustomText.Hello'), getText('Mods.test-localization.Deep.Level1.Level2'),
                         ModLocalization.Translate('CustomText.Hello'), getText('TestLocalization.Extra')];
            const extra = other === 'pt-BR' ? 'Extra BR' : 'Extra';
            return (got[0] === e.hello && got[1] === e.deep && got[2] === e.hello && got[3] === extra) || JSON.stringify(got);
        });
        check('{$chave} no outro idioma (' + other + ')', () => checkReferences(other));
        check('conteúdo e tooltip no outro idioma', () => checkRegisteredContent(other));
        check('nome de NPC e de item de mod no outro idioma' + (contentNames() ? '' : ' (sem o Example Mod: pulado)'),
            () => checkContentNames(other));
        check('o mesmo LocalizedText na troca', () =>
            held.Value === TEXTS[other].full || JSON.stringify(held.Value));
        check('Register com {$chave} no outro idioma', () => {
            const got = getText('TestLocalization.Ref');
            return got === getText(REFS + 'Name') + '!' || got;
        });
        check('volta ao idioma (' + culture + ')', () => {
            manager['void SetLanguage(string cultureName)'](culture);
            const e = expected();
            const got = [getText('Mods.test-localization.CustomText.Hello'), ModLocalization.ActiveCultureName];
            return (got[0] === e.hello && got[1] === culture) || JSON.stringify(got);
        });
        check('{$chave} de volta (' + culture + ')', () => checkReferences(culture));
        check('nome de NPC e de item de mod de volta', () => checkContentNames(culture));
        check('conteúdo e tooltip de volta', () => checkRegisteredContent(culture));
        check('trocas sucessivas com fallback', () => {
            try {
                for (const next of ['pt-BR', 'fr-FR', 'pt-BR', 'en-US']) {
                    manager['void SetLanguage(string cultureName)'](next);
                    const result = checkRegisteredContent(next);
                    if (result !== true) return next + ': ' + result;
                }
                return true;
            } finally {
                manager['void SetLanguage(string cultureName)'](culture);
            }
        });

        bl.log('localization FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}
