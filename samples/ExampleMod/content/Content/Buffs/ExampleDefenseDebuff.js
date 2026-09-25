import { ExamplePlayer } from '../Players/ExamplePlayer.js';

export class ExampleDefenseDebuff extends ModBuff {
    constructor() {
        super();
        this.Texture = 'Buffs/' + this.constructor.name;
        this.DefenseReductionPercent = 25;
        this.DefenseMultiplier = 1 - this.DefenseReductionPercent / 100;
    }

    SetStaticDefaults() {
        Terraria.Main.debuff[this.Type] = true;
    }

    ModifyDescription() {
        this.Description = this.Description.replace('{0}', this.DefenseReductionPercent);
    }

    UpdatePlayer(player, buffIndex) {
        const modPlayer = player.GetModPlayer(ExamplePlayer);
        modPlayer.ExampleDefenseDebuff = true;
        modPlayer.DefenseDebuffMultiplier = this.DefenseMultiplier;
    }
}
