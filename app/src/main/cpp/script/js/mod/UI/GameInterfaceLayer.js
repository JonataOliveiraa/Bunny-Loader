// Uma camada do desenho da interface (as do jogo se chamam "Vanilla: ...").
// No ModSystem.ModifyInterfaceLayers, Active = false esconde a camada neste
// quadro; uma camada nova desenha no DrawSelf, já com a escala do ScaleType.
class GameInterfaceLayer {
    Name = '';
    ScaleType = InterfaceScaleType.Game;
    Active = true;

    constructor(name, scaleType = InterfaceScaleType.Game) {
        this.Name = name;
        this.ScaleType = scaleType;
    }

    // true: as camadas seguintes desenham (false para a fila).
    DrawSelf() { return true; }
}
