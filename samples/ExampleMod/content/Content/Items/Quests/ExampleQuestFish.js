const { ItemID } = Terraria.ID;

// O peixe de missão do tModLoader (o de ponta-cabeça): entra na lista do
// Pescador e só aparece no modo difícil. Onde pescar: ExampleFishingPlayer.
export class ExampleQuestFish extends ModItem {
    SetStaticDefaults() {
        ItemID.Sets.CanBePlacedOnWeaponRacks[this.Type] = true;   // como os peixes do jogo
        ItemID.Sets.IsQuestFish[this.Type] = true;
    }

    SetDefaults() {
        this.Item.DefaultToQuestFish();
    }

    IsAnglerQuestAvailable() {
        return Terraria.Main.hardMode;
    }

    // O que o Pescador diz, e onde pescar (embaixo, entre parênteses).
    AnglerQuestChat(description, catchLocation) {
        description.value = ModLocalization.Translate('AnglerQuest.ExampleQuestFish.Description');
        catchLocation.value = ModLocalization.Translate('AnglerQuest.ExampleQuestFish.CatchLocation');
    }
}
