const { EmoteID } = Terraria.GameContent.UI;

// O emote do Exemplo de Item (o do tModLoader), na categoria de itens do menu
// de emotes.
export class ExampleItemEmote extends ModEmoteBubble {
    SetStaticDefaults() {
        this.AddToCategory(EmoteID.Category.Items);
    }
}
