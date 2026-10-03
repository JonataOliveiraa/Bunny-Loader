// Config. dos Mods com 14 mods (alfa ... november): a fileira de abas passa da
// largura das abas do jogo e rola de lado. Este, o primeiro, também tem os
// cabeçalhos com `color` e `align`, e confere a validação dos dois.
// O arraste e os cabeçalhos se conferem na tela; aqui loga
// "configtabs alfa <caso>: ok | FALHOU".
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('configtabs alfa ' + label + ': ok');
        else { fails++; bl.log('configtabs alfa ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('configtabs alfa ' + label + ': FALHOU com ' + e);
    }
}

export class AlfaConfig extends ModConfig {
    static Options = {
        CenterHeader: ModConfig.Header({ label: 'Centro, dourado (padrão)' }),
        A: ModConfig.Toggle(true, { label: 'Opção A' }),
        LeftHeader: ModConfig.Header({ label: 'Esquerda, azul', align: 'left', color: '#7fd4ff' }),
        B: ModConfig.Toggle(false, { label: 'Opção B' }),
        RightHeader: ModConfig.Header({ label: 'Direita, vermelho', align: 'right', color: '#FF6060' }),
        C: ModConfig.Toggle(true, { label: 'Opção C' }),
        GreenHeader: ModConfig.Header({ label: 'Centro, verde', color: '#60FF80' }),
        D: ModConfig.Toggle(true, { label: 'Opção D' }),
    };
}

const rejects = (options, type) => () => {
    class Bad extends ModConfig { static Options = options; }
    try { ModConfig.register(Bad); } catch (e) { return e instanceof type || String(e); }
    return 'aceitou';
};
check('Header com cor inválida lança TypeError', rejects({ H: ModConfig.Header({ color: 'azul' }) }, TypeError));
check('Header com cor curta lança TypeError', rejects({ H: ModConfig.Header({ color: '#FFF' }) }, TypeError));
check('Header com align inválido lança RangeError', rejects({ H: ModConfig.Header({ align: 'middle' }) }, RangeError));
check('Header com cor e align válidos é aceito', () => {
    class Good extends ModConfig { static Options = { H: ModConfig.Header({ label: 'Registrada à mão: esquerda, #abcdef', color: '#abcdef', align: 'left' }) }; }
    return ModConfig.register(Good) instanceof Good;
});
bl.log('configtabs alfa FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));

export default class TestConfigTabsAlfa extends Mod {}
