import { ExampleLifeFruit } from '../../Content/Items/Consumables/ExampleLifeFruit.js';
import { ExampleManaCrystal } from '../../Content/Items/Consumables/ExampleManaCrystal.js';
import { MessageType } from '../Networking/MessageType.js';

// Quantas frutas da vida e cristais de mana de exemplo o jogador já comeu: a
// vida e a mana máximas sobem por eles (ModifyMaxStats) e o número fica no
// save do personagem.
export class ExampleStatIncreasePlayer extends ModPlayer {
    exampleLifeFruits = 0;
    exampleManaCrystals = 0;

    ModifyMaxStats(player) {
        this.CumulativeHealth = this.exampleLifeFruits * ExampleLifeFruit.LifePerFruit;
        this.CumulativeMana = this.exampleManaCrystals * ExampleManaCrystal.ManaPerCrystal;
    }

    // No multijogador os outros precisam dos números, senão calculam a vida
    // máxima deste jogador sem as frutas.
    SyncPlayer(player, toWho, fromWho, newPlayer) {
        const packet = this.Mod.GetPacket();
        packet.Write(MessageType.ExampleStatIncreasePlayerSync);
        packet.Write(player.whoAmI);
        packet.Write(this.exampleLifeFruits);
        packet.Write(this.exampleManaCrystals);
        packet.Send(toWho, fromWho);
    }

    // Chamado pelo HandlePacket do main.js.
    ReceivePlayerSync(reader) {
        this.exampleLifeFruits = reader.ReadByte();
        this.exampleManaCrystals = reader.ReadByte();
    }

    CopyClientState(player, targetCopy) {
        targetCopy.exampleLifeFruits = this.exampleLifeFruits;
        targetCopy.exampleManaCrystals = this.exampleManaCrystals;
    }

    // Comeu mais uma: manda tudo de novo (com muitos dados, valeria mandar só o que mudou).
    SendClientChanges(player, clientPlayer) {
        if (this.exampleLifeFruits !== clientPlayer.exampleLifeFruits || this.exampleManaCrystals !== clientPlayer.exampleManaCrystals) {
            this.SyncPlayer(player, -1, Terraria.Main.myPlayer, false);
        }
    }

    SaveData(tag) {
        tag.exampleLifeFruits = this.exampleLifeFruits;
        tag.exampleManaCrystals = this.exampleManaCrystals;
    }

    LoadData(tag) {
        this.exampleLifeFruits = tag.GetInt('exampleLifeFruits');
        this.exampleManaCrystals = tag.GetInt('exampleManaCrystals');
    }
}
