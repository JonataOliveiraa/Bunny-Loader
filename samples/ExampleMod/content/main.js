// Example Mod: uma parte do ExampleMod do tModLoader, refeita para o Bunny Loader.
//
// A mesma estrutura do tModLoader:
//
//   main.js          esta classe, a do mod (obrigatória)
//   Assets/          Textures/ (fundos, tema do menu), Sounds/ e Music/
//   Common/          o que não é coisa nova: ModPlayer, ModSystem, Global*
//   Content/         itens, NPCs, projéteis, blocos, buffs, pets... (com as texturas)
//   Localization/    en-US.json, pt-BR.json...
//
// Toda classe exportada em Content/ e Common/ é registrada sozinha, e a
// textura dela fica ao lado do arquivo, como no tModLoader
// (Content/Items/ExampleItem.js -> Content/Items/ExampleItem.png). Em
// Assets/ fica o resto: fundos, sons, música e o tema do menu.
import { MessageType } from './Common/Networking/MessageType.js';
import { ExampleStatIncreasePlayer } from './Common/Players/ExampleStatIncreasePlayer.js';
import { ExampleResourcePlayer } from './Common/Players/ExampleResourcePlayer.js';

export default class ExampleMod extends Mod {
    // Uma nuvem só de textura, com peso 0,1 contra as 22 comuns do jogo (o
    // DefaultCloudsLoader do tModLoader). Sem isso, o PNG em Clouds/ viraria
    // nuvem de peso 1 sozinho.
    Load() {
        CloudLoader.AddCloudFromTexture('Content/Clouds/ExampleCloud', 0.1, false);
    }

    // Os pacotes do mod (o ExampleMod.Networking.cs do tModLoader). O
    // primeiro valor é o MessageType.
    HandlePacket(reader, whoAmI) {
        const Main = Terraria.Main;
        const msgType = reader.ReadByte();

        switch (msgType) {
            // As frutas e cristais de um jogador (ExampleStatIncreasePlayer).
            case MessageType.ExampleStatIncreasePlayerSync: {
                const playerNumber = reader.ReadByte();
                const player = Main.player[playerNumber];
                const examplePlayer = player.GetModPlayer(ExampleStatIncreasePlayer);
                examplePlayer.ReceivePlayerSync(reader);

                // O servidor repassa aos outros clientes.
                if (Main.netMode & 2) examplePlayer.SyncPlayer(player, -1, whoAmI, false);
                break;
            }
            case MessageType.ExampleResourceEffect:
                ExampleResourcePlayer.HandleExampleResourceEffectMessage(reader, whoAmI);
                break;
            default:
                bl.log('ExampleMod: Unknown Message type: ' + msgType);
                break;
        }
    }

    PostSetupContent() {
        bl.log(`Example Mod: ExampleItem = ${ModItem.getTypeByName('ExampleItem')}, ` +
               `Espada = ${ModItem.getTypeByName('ExampleMeleeWeapon')}, ` +
               `Bala = ${ModItem.getTypeByName('ExampleBullet')}, ` +
               `Arma = ${ModItem.getTypeByName('ExampleGun')}, ` +
               `projetil da bala = ${ModProjectile.getTypeByName('ExampleBulletProjectile')}, ` +
               `slime = ${ModNPC.getTypeByName('ExampleSlimeNPC')}`);
    }
}
