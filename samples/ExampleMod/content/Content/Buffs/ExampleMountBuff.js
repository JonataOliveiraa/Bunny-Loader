import { ExampleMount } from '../Mounts/ExampleMount.js';

// O buff do carro: sem tempo na tela e sem salvar; enquanto ativo, mantém o
// jogador montado.
export class ExampleMountBuff extends ModBuff {
    SetStaticDefaults() {
        Terraria.Main.buffNoTimeDisplay[this.Type] = true;
        Terraria.Main.buffNoSave[this.Type] = true;
    }

    UpdatePlayer(player, buffIndex) {
        player.mount['void SetMount(int m, Player mountedPlayer, bool ignoreEffect)'](ModContent.MountType(ExampleMount), player, false);
        player.buffTime[buffIndex] = 10;
    }
}
