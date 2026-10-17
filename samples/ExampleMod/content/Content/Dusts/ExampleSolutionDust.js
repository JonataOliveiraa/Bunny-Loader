const { DustID } = Terraria.ID;

// Copia o comportamento da poeira da Solução Pura do jogo (UpdateType), com a
// cor da textura própria. É a poeira da água do ExampleMod.
export class ExampleSolutionDust extends ModDust {
    SetStaticDefaults() {
        this.UpdateType = DustID.PureSpray;
    }
}
