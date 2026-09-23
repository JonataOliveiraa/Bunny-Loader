// Vida Cheia — a vida volta ao máximo a cada quadro.
//
// Player.Update é o passo do jogador dentro do quadro. Chamamos o original
// primeiro (o jogo aplica dano, veneno, fome) e só então repomos: assim os
// efeitos continuam acontecendo, o jogador é que não morre.
//
// statLifeMax2 é o máximo COM os bônus de acessório; statLifeMax é o cru.
// Usar o cru tiraria vida de quem tem Coração de Ferro equipado.

const Update = Terraria.Player['void Update(int i)'];

Update.hook((original, self, i) => {
    original(self, i);
    if (self.statLife < self.statLifeMax2) {
        self.statLife = self.statLifeMax2;
    }
});

bl.log('Vida Cheia: ativo');
