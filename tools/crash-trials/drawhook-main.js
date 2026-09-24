// Minimo: so um hook repassando Main.DoDraw ao original.
Terraria.Main['void DoDraw(GameTime gameTime)'].hook((o, self, gt) => o(self, gt));
bl.log('drawhook: ativo');
