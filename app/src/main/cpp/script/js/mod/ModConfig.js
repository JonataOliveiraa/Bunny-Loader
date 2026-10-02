// As opções de um mod, como o ModConfig do tModLoader: aparecem na tela
// "Config. dos Mods" (menu de pausa), cada mod numa seção, e ficam salvas no
// aparelho (a pasta de dados do mod, um <Classe>.json por config).
//
//   export class ExampleConfig extends ModConfig {
//       static Options = {
//           ShowTrail: ModConfig.Toggle(true),
//           SpawnRate: ModConfig.Range(100, { min: 0, max: 200, step: 10, suffix: '%' }),
//           Difficulty: ModConfig.Radio('normal', ['easy', 'normal', 'hard']),
//       };
//       OnChanged(key) { ... }
//   }
//
//   ModContent.GetInstance(ExampleConfig).SpawnRate   // o valor atual
//
// Os textos saem do Localization/<cultura>.json do mod, como no tModLoader:
// Configs.<Classe>.<Opção>.Label, e Configs.<Classe>.<Opção>.<escolha> para
// cada escolha do Radio. Sem eles, o `label` da opção, ou o nome separado
// ("SpawnRate" vira "Spawn Rate"), e a escolha como foi escrita.
//
// Por enquanto as opções são do aparelho (o ClientSide do tModLoader): no
// multijogador cada jogador tem as suas.
class ModConfig {
    // O valor atual de cada opção é uma propriedade da instância.
    OnLoaded() {}
    OnChanged(key) {}

    static Toggle(defaultValue = false, extra = {}) {
        return { ...extra, type: 'toggle', default: !!defaultValue };
    }

    static Range(defaultValue, { min = 0, max = 100, step = 1, suffix = '', ...extra } = {}) {
        return { ...extra, type: 'range', default: defaultValue, min, max, step, suffix };
    }

    static Radio(defaultValue, choices, extra = {}) {
        return { ...extra, type: 'radio', default: defaultValue, choices: [...choices] };
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModConfig)) {
            throw new TypeError('ModConfig.register(Classe): passe a classe, que estende ModConfig');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        ConfigLoader.Add(inst);
        return inst;
    }
}
