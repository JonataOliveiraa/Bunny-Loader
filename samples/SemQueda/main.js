// Sem Dano de Queda — a queda nunca "começa".
//
// O Terraria mede a queda pela diferença entre o tile onde ela começou
// (fallStart) e onde o jogador parou. Colando fallStart na posição atual a
// cada quadro, a conta dá sempre zero.
//
// position é um Vector2 do jogo, e `self.position.Y` lê direto o campo dentro
// do objeto — não é cópia. Dividir por 16 converte pixel em tile.

const Update = Terraria.Player['void Update(int i)'];

Update.hook((original, self, i) => {
    original(self, i);
    const tileY = Math.floor(self.position.Y / 16);
    self.fallStart = tileY;
    self.fallStart2 = tileY;
});

bl.log('Sem Dano de Queda: ativo');
