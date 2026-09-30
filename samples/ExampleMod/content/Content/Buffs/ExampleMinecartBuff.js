import { ExampleMinecartMount } from '../Mounts/ExampleMinecartMount.js';

// O buff do carrinho. Com BuffID.Sets.MountType, o próprio jogo monta o
// jogador enquanto o buff durar, e o carregador de montarias o deixa sem tempo
// na tela e sem salvar.
export class ExampleMinecartBuff extends ModBuff {
    SetStaticDefaults() {
        Terraria.ID.BuffID.Sets.MountType[this.Type] = ModContent.MountType(ExampleMinecartMount);
    }
}
