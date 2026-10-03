// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class LimaConfig extends ModConfig {
    static Options = {
        LimaHeader: ModConfig.Header({ label: 'Opções de Lima' }),
        LimaToggle: ModConfig.Toggle(true, { label: 'Ligado em Lima' }),
    };
}
bl.log('configtabs lima FIM: tudo ok');

export default class TestConfigTabsLima extends Mod {}
