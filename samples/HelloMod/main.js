// Hello Mod — turbina a Minishark (firerate + velocidade do projetil).
//
// Conferido no dump de Terraria 1.4.5.6.4: a assinatura e
//   Item.SetDefaults(int Type, ItemVariant variant)   (2 params, instancia)
// A wiki do TL Pro mostra (int Type, bool noMatCheck) da 1.4.0.5.2.1 — mudou.
//
// API do Bunny Loader disponivel nesta versao:
//   new NativeClass(ns, nome)          -> classe
//   cls.getStaticInt/Float(campo)      -> ler estatico
//   cls.setStaticInt/Float(campo, v)   -> escrever estatico
//   cls.new()                          -> instancia (NativeObject)
//   cls.method(nome, nParams)          -> NativeMethod
//   metodo.hook((original, self, ...args) => { ... })
//   obj.getInt/Float(campo), obj.setInt/Float(campo, v)

const Item = new NativeClass('Terraria', 'Item');
const ItemID = new NativeClass('Terraria.ID', 'ItemID');

const MINISHARK = ItemID.getStaticInt('Minishark');
tl.log('HelloMod: Minishark = ' + MINISHARK + '; hookando Item.SetDefaults');

const SetDefaults = Item.method('SetDefaults', 2);

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);        // deixa o jogo aplicar os defaults
    if (type === MINISHARK) {
        self.setInt('useTime', 4);        // cadencia (menor = mais rapido)
        self.setInt('useAnimation', 4);
        self.setFloat('shootSpeed', 10.0); // velocidade do projetil
        tl.log('HelloMod: Minishark turbinada!');
    }
});
