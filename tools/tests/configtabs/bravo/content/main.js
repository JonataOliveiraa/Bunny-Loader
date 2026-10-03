// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class BravoConfig extends ModConfig {
    static Options = {
        BravoHeader: ModConfig.Header({ label: 'Opções de Bravo' }),
        BravoToggle: ModConfig.Toggle(true, { label: 'Ligado em Bravo' }),
    };
}
bl.log('configtabs bravo FIM: tudo ok');

export default class TestConfigTabsBravo extends Mod {}
