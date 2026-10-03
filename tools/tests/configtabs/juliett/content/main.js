// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class JuliettConfig extends ModConfig {
    static Options = {
        JuliettHeader: ModConfig.Header({ label: 'Opções de Juliett' }),
        JuliettToggle: ModConfig.Toggle(true, { label: 'Ligado em Juliett' }),
    };
}
bl.log('configtabs juliett FIM: tudo ok');

export default class TestConfigTabsJuliett extends Mod {}
