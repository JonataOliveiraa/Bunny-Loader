// Example Mod — dois itens NOVOS, e nao um item do jogo mudado.
//
// A parte de itens do ExampleMod do TL Pro (ExampleItem e ExampleMeleeWeapon),
// que vem do ExampleMod do tModLoader. As texturas sao as dele.
//
// ============================== bl.items ==============================
//
//   const type = bl.items.register({
//       name: 'MySword',                   // chave estavel dentro do mod
//       texture: 'Textures/MySword.png',   // relativo a este main.js
//       displayName: 'Espada',             // ou { 'pt-BR': ..., 'en-US': ... }
//       setDefaults(item) { ... },         // como o SetDefaults de um ModItem
//   });
//
// O tipo sai NA HORA — e ItemID.Count mais a ordem de registro, o mesmo a
// cada boot com os mesmos mods — e ja serve para comparar e para dar o item.
// O jogo passa a conhecer o item na tela de titulo.
//
// `setDefaults(item)` recebe o Item do jogo JA ZERADO (o jogo rodou
// ResetStats), com `item.type` certo. Escreva direto nos campos, como em C#.
// Largura e altura, se voce nao disser, saem do tamanho da textura.
//
// ============================== bl.menu ===============================
//
// Todo item registrado aparece no Mod Menu, na categoria do mod (nome e
// icon.png do manifesto). Para mais categorias:
//
//   const weapons = bl.menu.itemCategory('Armas do mod', 'Textures/icon.png');
//   bl.menu.addItem(weapons, type);
//
// ============================ o que falta =============================
//
// Item de mod ainda NAO sobrevive a salvar: o jogo, ao carregar o personagem,
// troca por nada todo tipo que ele nao conhece. Receita, tooltip e projetil
// proprio tambem ainda nao.

const { SoundID, ItemUseStyleID } = Terraria.ID;

// Raridade verde. O ExMod usa ItemRarityID.Green, mas essa classe nao existe
// neste Terraria — o ExMod traz a dele (TL/Enums). Aqui vai o numero.
const RARITY_GREEN = 2;

// Moeda do jogo em cobre: 1 prata = 100, 1 ouro = 10000.
const SILVER = 100;
const GOLD = 10000;

// ExampleItem: um material, sem uso. So empilha e vale 1 prata na compra.
const exampleItem = bl.items.register({
    name: 'ExampleItem',
    texture: 'Textures/ExampleItem.png',
    displayName: { 'pt-BR': 'Exemplo de Item', 'en-US': 'Example Item' },
    setDefaults(item) {
        item.maxStack = 9999;
        item.value = 1 * SILVER;
    },
});

// ExampleMeleeWeapon: a espada. Os numeros sao os do ExampleMod.
const sword = bl.items.register({
    name: 'ExampleMeleeWeapon',
    texture: 'Textures/ExampleMeleeWeapon.png',
    displayName: { 'pt-BR': 'Espada', 'en-US': 'Sword' },
    setDefaults(item) {
        item.melee = true;
        item.damage = 50;
        item.knockBack = 6;
        item.crit = 6;
        item.useTime = 20;
        item.useAnimation = 20;
        item.autoReuse = true;
        item.useStyle = ItemUseStyleID.Swing;
        // O jogo vende pelo value / 5: 1 ouro de venda sao 5 de valor.
        item.value = 5 * GOLD;
        item.rare = RARITY_GREEN;
        item.UseSound = SoundID.Item1;
    },
});

bl.log(`Example Mod: ExampleItem = ${exampleItem}, Espada = ${sword}`);
