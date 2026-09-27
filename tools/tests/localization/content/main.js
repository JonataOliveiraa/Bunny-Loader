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
    'en-US': { hello: 'Hello', deep: 'Deep text' },
    'pt-BR': { hello: 'Olá', deep: 'Texto fundo' },
};
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

        // A troca de idioma volta tudo para a chave e recarrega só o jogo.
        const other = culture === 'pt-BR' ? 'en-US' : 'pt-BR';
        const manager = Terraria.Localization.LanguageManager.Instance;
        check('troca de idioma (' + other + ')', () => {
            manager['void SetLanguage(string cultureName)'](other);
            const e = TEXTS[other];
            const got = [getText('Mods.test-localization.CustomText.Hello'), getText('Mods.test-localization.Deep.Level1.Level2'),
                         ModLocalization.Translate('CustomText.Hello'), getText('TestLocalization.Extra')];
            const extra = other === 'pt-BR' ? 'Extra BR' : 'Extra';
            return (got[0] === e.hello && got[1] === e.deep && got[2] === e.hello && got[3] === extra) || JSON.stringify(got);
        });
        check('volta ao idioma (' + culture + ')', () => {
            manager['void SetLanguage(string cultureName)'](culture);
            const e = expected();
            const got = [getText('Mods.test-localization.CustomText.Hello'), ModLocalization.ActiveCultureName];
            return (got[0] === e.hello && got[1] === culture) || JSON.stringify(got);
        });

        bl.log('localization FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}
