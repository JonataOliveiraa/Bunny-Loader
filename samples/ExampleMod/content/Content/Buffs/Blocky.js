import { ExampleCostumePlayer } from '../../Common/Players/ExampleCostumePlayer.js';

const { BuffID } = Terraria.ID;

// O buff do ExampleCostume: pulo alto e queda sem dano, enquanto houver
// morador por perto e o fantasia estiver equipado.
export class Blocky extends ModBuff {
    SetStaticDefaults() {
        const Main = Terraria.Main;
        Main.debuff[this.Type] = true;
        Main.buffNoSave[this.Type] = true;
        Main.buffNoTimeDisplay[this.Type] = true;
        BuffID.Sets.NurseCannotRemoveDebuff[this.Type] = true;
    }

    UpdatePlayer(player, buffIndex) {
        const p = player.GetModPlayer(ExampleCostumePlayer);
        if (player.townNPCs >= 1 && p.BlockyAccessoryPrevious) {
            p.BlockyPower = true;
            player.jumpSpeedBoost += 4.8;
            player.extraFall += 45;
        } else {
            player.DelBuff(buffIndex);
        }
    }
}
