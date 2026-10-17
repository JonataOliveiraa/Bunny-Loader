// Os números da vida e da mana do jogador local neste quadro (o
// PlayerStatsSnapshot do jogo): quantos corações e estrelas há, e quanto vale cada um.
class PlayerStatsSnapshot {
    Life = 0;
    LifeMax = 0;
    LifeFruitCount = 0;
    LifePerSegment = 20;
    Mana = 0;
    ManaMax = 0;
    ManaPerSegment = 20;
    AmountOfLifeHearts = 0;
    AmountOfManaStars = 0;

    constructor(player) {
        this.Life = player.statLife;
        this.Mana = player.statMana;
        this.LifeMax = player.statLifeMax2;
        this.ManaMax = player.statManaMax2;

        // A mesma conta do jogo: acima de 400, as frutas encolhem o valor de cada coração.
        const base = player.statLifeMax;
        let perSegment = 20;
        let hearts = Math.max(1, Math.trunc(base / 20));
        const fruits = Math.max(0, Math.trunc((base - 400) / 5));
        if (fruits > 0) {
            hearts = Math.max(1, Math.trunc(base / (20 + fruits / 4)));
            perSegment = base / 20;
        }
        perSegment += (player.statLifeMax2 - base) / hearts;
        this.LifeFruitCount = fruits;
        this.LifePerSegment = perSegment;
        this.AmountOfLifeHearts = Math.trunc(this.LifeMax / perSegment);
        this.AmountOfManaStars = Math.trunc(this.ManaMax / this.ManaPerSegment);
    }
}
