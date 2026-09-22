// Hello Mod — turbina a Minishark (cadência + velocidade do projétil).
//
// API do Bunny Loader:
//
//   new NativeClass(ns, nome)              -> classe
//   Classe['ret Nome(T a, U b)']           -> método, por ASSINATURA (recomendado)
//   Classe.Nome                            -> método, se houver um só overload
//   Classe.campoEstatico                   -> ler campo estático
//   Classe.campoEstatico = v               -> escrever campo estático
//   Classe.new()                           -> instância (NativeObject)
//   metodo.hook((original, self, ...args) => { ... })
//   obj.getInt/getFloat(campo), obj.setInt/setFloat(campo, v)
//   bl.log(...)
//
// A assinatura vem do dump, copiada como está lá. Prefira-a ao nome puro:
// nome + contagem de parâmetros não desambigua overloads, e escolher o errado
// é como o Item.NewItem dava exceção a cada frame.

const Item = new NativeClass('Terraria', 'Item');
const ItemID = new NativeClass('Terraria.ID', 'ItemID');

const MINISHARK = ItemID.Minishark;
bl.log('HelloMod: Minishark = ' + MINISHARK + '; hookando Item.SetDefaults');

const SetDefaults = Item['void SetDefaults(int Type, ItemVariant variant)'];

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);        // deixa o jogo aplicar os defaults
    if (type === MINISHARK) {
        self.setInt('useTime', 4);        // cadência (menor = mais rápido)
        self.setInt('useAnimation', 4);
        self.setFloat('shootSpeed', 10.0); // velocidade do projétil
        bl.log('HelloMod: Minishark turbinada!');
    }
});
