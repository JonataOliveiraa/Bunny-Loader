// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class FoxtrotConfig extends ModConfig {
    static Options = {
        FoxtrotHeader: ModConfig.Header({ label: 'Opções de Foxtrot' }),
        FoxtrotToggle: ModConfig.Toggle(true, { label: 'Ligado em Foxtrot' }),
    };
}
bl.log('configtabs foxtrot FIM: tudo ok');

export default class TestConfigTabsFoxtrot extends Mod {}
