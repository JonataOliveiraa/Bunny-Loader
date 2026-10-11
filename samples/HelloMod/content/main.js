const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];
export default class HelloMod extends Mod {
    Load() {
        bl.log('HelloMod: Minishark = ' + Terraria.ID.ItemID.Minishark + '; hookando Item.SetDefaults');
        SetDefaults.hook((original, self, type, variant) => {
            original(self, type, variant); 
            if (type === Terraria.ID.ItemID.Minishark) {
                self.useTime = 4;            // cadência (menor = mais rápido)
                self.useAnimation = 4;
                self.shootSpeed = 10.0;      // float: o tipo do campo decide a escrita
                bl.log('HelloMod: Minishark turbinada!');
            }
        });
    }
}
