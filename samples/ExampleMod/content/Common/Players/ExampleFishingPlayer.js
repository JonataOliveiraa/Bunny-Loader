import { ExampleQuestFish } from '../../Content/Items/Quests/ExampleQuestFish.js';

// A pesca do ExampleMod (o ExampleFishingPlayer do tModLoader, só a parte do
// peixe de missão): quando a missão do dia é o ExampleQuestFish, ele sai num
// sorteio incomum com o jogador de cabeça para baixo.
export class ExampleFishingPlayer extends ModPlayer {
    CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition) {
        const fish = ModContent.ItemType(ExampleQuestFish);
        if (attempt.questFish === fish && this.Player.gravDir < 0 && attempt.uncommon) {
            itemDrop.value = fish;
        }
    }
}
