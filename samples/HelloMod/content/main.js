const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];
export default class HelloMod extends Mod {
    Load() {
        SetDefaults.hook((original, self, type, variant) => {
            original(self, type, variant); 
            if (type === Terraria.ID.ItemID.Minishark) {
                self.useTime = 4;
                self.useAnimation = 4;
                self.shootSpeed = 10.0;
            }
        });
    }
}
