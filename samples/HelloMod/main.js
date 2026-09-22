// Hello Mod — alvo de aceite da Fase 4.
//
// Assinaturas conferidas contra o dump de Terraria 1.4.5.6.4 (versionCode
// 301543). ATENÇÃO: a wiki do TL Pro documenta a 1.4.0.5.2.1, onde a assinatura
// era `SetDefaults(int Type, bool noMatCheck)`. Essa sobrecarga NÃO existe mais
// neste build — virou `SetDefaults(int Type, ItemVariant variant)`.

// Etapa 1 (só precisa de tl.log): prova que o QuickJS roda dentro do jogo.
tl.log('Hello Mod carregado!');

// Etapa 2 (precisa de NativeClass + hook): altera o firerate e a velocidade do
// projétil da Minishark. Descomente quando os bindings existirem.
//
// const Item   = new NativeClass('Terraria', 'Item');
// const ItemID = new NativeClass('Terraria.ID', 'ItemID');
//
// // Minishark = 98 (Terraria.ID.ItemID)
// const SetDefaults = Item['void SetDefaults(int Type, ItemVariant variant)'];
//
// SetDefaults.hook((original, self, type, variant) => {
//     original(self, type, variant);
//     if (type === ItemID.Minishark) {
//         self.useTime      = 4;   // 0x5C
//         self.useAnimation = 4;   // 0x58
//         self.shootSpeed   = 10.0; // 0xFC
//     }
// });
