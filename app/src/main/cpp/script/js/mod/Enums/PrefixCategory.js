// Que itens podem ganhar um prefixo, como o PrefixCategory do tModLoader.
// Custom só aparece onde o próprio prefixo (CanRoll) ou o item (ChoosePrefix) deixar.
const PrefixCategory = Object.freeze({
    Melee: 0,       // espadas, martelos, machados, picaretas: mexe no tamanho
    Ranged: 1,      // armas e arcos: mexe na velocidade do tiro
    Magic: 2,       // mágicas: mexe no custo de mana
    Summon: 3,
    AnyWeapon: 4,   // os de toda arma
    Accessory: 5,
    Custom: 6,
});
