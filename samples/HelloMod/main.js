// Hello Mod — alvo de aceite da Fase 4.
//
// Etapa 1 (só precisa de tl.log): prova que o QuickJS roda dentro do jogo.
tl.log('Hello Mod carregado!');

// Etapa 2 (precisa de NativeClass + hook): altera o firerate e a velocidade
// do projétil da Minishark. Descomente quando os bindings existirem.
//
// const Item   = new NativeClass('Terraria', 'Item');
// const ItemID = new NativeClass('Terraria.ID', 'ItemID');
// const SetDefaults = Item['void SetDefaults(int Type, bool noMatCheck)'];
//
// SetDefaults.hook((original, self, type, noMatCheck) => {
//     original(self, type, noMatCheck);
//     if (type === ItemID.Minishark) {
//         self.useTime      = 4;
//         self.useAnimation = 4;
//         self.shootSpeed   = 10.0;
//     }
// });
