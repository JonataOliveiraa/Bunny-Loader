// Hello Mod — turbina a Minishark (cadência + velocidade do projétil).
//
// ================================ API ================================
//
//   Terraria.Item                    classe, pela árvore de namespaces
//   Terraria.ID.ItemID.Minishark     campo estático como propriedade
//   Classe['ret Nome(T a, U b)']     método, por ASSINATURA (recomendado)
//   Classe.Nome                      método, se houver um só overload
//   obj.campo / obj.campo = v        campo de instância como propriedade
//   metodo.hook((original, self, ...args) => { ... })
//   bl.log(...)
//   bl.classOf(ns, nome)             escape hatch, quando a árvore não ajuda
//
// Chamar método do jogo — o objeto é `this`, como em JS:
//
//   Terraria.Main['int get_myPlayer()']()           // estático
//   item['void SetDefaults(int Type, ItemVariant variant)'](98, null)
//
// Criar objeto, equivalente a `new Item()` / `new Vector2(5,10)` em C#:
//
//   const it = Terraria.Item.new();
//   it['void .ctor()']();
//
//   const v = Microsoft.Xna.Framework.Vector2.new();
//   v['void .ctor(float x, float y)'](5.0, 10.0);   // struct também
//
// A assinatura vem do dump, copiada como está lá. Prefira-a ao nome puro:
// nome + contagem de parâmetros não desambigua overloads. Quando o nome é
// ambíguo o loader RECUSA e lista os overloads, em vez de escolher um.
//
// Método do jogo que lança vira exceção JS — não é engolido.

const MINISHARK = Terraria.ID.ItemID.Minishark;
bl.log('HelloMod: Minishark = ' + MINISHARK + '; hookando Item.SetDefaults');

const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);   // deixa o jogo aplicar os defaults
    if (type === MINISHARK) {
        self.useTime = 4;            // cadência (menor = mais rápido)
        self.useAnimation = 4;
        self.shootSpeed = 10.0;      // float: o tipo do campo decide a escrita
        bl.log('HelloMod: Minishark turbinada!');
    }
});
