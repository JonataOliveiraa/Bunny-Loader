// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class MikeConfig extends ModConfig {
    static Options = {
        MikeHeader: ModConfig.Header({ label: 'Opções de Mike' }),
        MikeToggle: ModConfig.Toggle(true, { label: 'Ligado em Mike' }),
    };
}
bl.log('configtabs mike FIM: tudo ok');

export default class TestConfigTabsMike extends Mod {}
