// Config. dos Mods no menu principal: a linha no topo da aba Geral das
// Configurações abre a tela. Os toques são dados de fora (adb) e a
// conferência é pela tela e pelo log:
//   no menu: as duas configs (Mundo e Geral);
//   no mundo (menu de pausa): só a Geral.
export class WorldGenConfig extends ModConfig {
    static VisibleInWorld = false;

    static Options = {
        WorldHeader: ModConfig.Header({ label: 'Geração de mundo (só no menu)' }),
        BigCaves: ModConfig.Toggle(false, { label: 'Cavernas grandes' }),
        OreAmount: ModConfig.Range(100, { min: 50, max: 200, step: 10, suffix: '%', label: 'Minério' }),
    };

    OnApply() { bl.log('configmenu OnApply mundo BigCaves=' + this.BigCaves + ' OreAmount=' + this.OreAmount); }
}

export class GeneralConfig extends ModConfig {
    static Options = {
        GeneralHeader: ModConfig.Header({ label: 'Geral (menu e mundo)' }),
        Flag: ModConfig.Toggle(false, { label: 'Interruptor' }),
    };

    OnApply() { bl.log('configmenu OnApply geral Flag=' + this.Flag); }
}

bl.log('configmenu FIM: carregado');

export default class TestConfigMenu extends Mod {}
