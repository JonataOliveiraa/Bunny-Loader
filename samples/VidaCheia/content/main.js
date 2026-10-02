const Update = Terraria.Player['void Update(int i)'];

export default class VidaCheia extends Mod {
    Load() {
        Update.hook((original, self, i) => {
            original(self, i);
            if (self.statLife < self.statLifeMax2) {
                self.statLife = self.statLifeMax2;
            }
        });
        bl.log('Vida Cheia: ativo');
    }
}
