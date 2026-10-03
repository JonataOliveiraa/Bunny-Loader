// A Config. dos Mods mexe num rascunho. Este mod só registra o que a config
// vê; os toques são dados de fora (adb) e a conferência é pelo log:
//   "configapply valor Flag=... Level=..."  sempre que o valor real muda;
//   "configapply OnChanged <chave>" e "configapply OnApply".
export class ApplyConfig extends ModConfig {
    static Options = {
        MainHeader: ModConfig.Header({ label: 'Rascunho' }),
        Flag: ModConfig.Toggle(false, { label: 'Interruptor' }),
        Level: ModConfig.Cycle('one', ['one', 'two', 'three'], { label: 'Nível' }),
        Bump: ModConfig.Button('BumpLevel', { label: 'Botão: próximo nível', text: 'Mudar' }),
    };

    BumpLevel() {
        const next = { one: 'two', two: 'three', three: 'one' };
        this.SetOption('Level', next[this.Level]);
    }

    OnChanged(key) { bl.log('configapply OnChanged ' + key); }
    OnApply() { bl.log('configapply OnApply'); }
}

let last = '';
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Terraria.Main.myPlayer || Terraria.Main.gameMenu) return;
    self.statLife = self.statLifeMax2;
    const config = ModContent.GetInstance(ApplyConfig);
    const now = 'Flag=' + config.Flag + ' Level=' + config.Level;
    if (now !== last) {
        last = now;
        bl.log('configapply valor ' + now);
    }
});
bl.log('configapply FIM: carregado');

export default class TestConfigApply extends Mod {}
