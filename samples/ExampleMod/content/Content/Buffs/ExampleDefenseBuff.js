const LocalizedDuration = Terraria.Lang['string LocalizedDuration(TimeSpan time, bool abbreviated, bool showAllAvailableUnits)'];
const FromSeconds = System.TimeSpan['TimeSpan FromSeconds(double value)'];

export class ExampleDefenseBuff extends ModBuff {
    constructor() {
        super();
        this.DefenseBonus = 10;
    }

    ModifyDescription() {
        this.Description = this.Description.replace('{0}', this.DefenseBonus);
    }

    UpdatePlayer(player, buffIndex) {
        player.statDefense += this.DefenseBonus;
    }

    // O nome e a dica ao tocar no buff, montados a cada vez: aqui a dica ganha
    // o tempo que falta ("1 minuto 30 segundos"). buffName, tip e rare são Ref.
    ModifyBuffText(buffName, tip, rare) {
        const player = Terraria.Main.player[Terraria.Main.myPlayer];
        const index = player.FindBuffIndex(this.Type);
        if (index < 0) return;

        const time = player.buffTime[index];
        if (time <= 2) return;
        const text = LocalizedDuration(FromSeconds(Math.floor(time / 60)), false, true);
        tip.value += '\n' + ModLocalization.Translate('CustomText.RemainingTime').replace('{0}', text);
    }
}
