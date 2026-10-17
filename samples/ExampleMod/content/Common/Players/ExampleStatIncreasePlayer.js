import { ExampleLifeFruit } from '../../Content/Items/Consumables/ExampleLifeFruit.js';
import { ExampleManaCrystal } from '../../Content/Items/Consumables/ExampleManaCrystal.js';

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

    SaveData(tag) {
        tag.exampleLifeFruits = this.exampleLifeFruits;
        tag.exampleManaCrystals = this.exampleManaCrystals;
    }

    LoadData(tag) {
        this.exampleLifeFruits = tag.GetInt('exampleLifeFruits');
        this.exampleManaCrystals = tag.GetInt('exampleManaCrystals');
    }
}
