const { ItemID, SoundID } = Terraria.ID;

// Um item de coleta ("pickup"): não entra no inventário, faz algo ao ser
// pego. Como os corações (vida) e as estrelas (mana). Este dá 10 segundos
// de Defesa (ExampleDefenseBuff). Cai dos inimigos como os corações:
// Common/GlobalNPCs/ExamplePickupGlobalNPC.js.
export class ExamplePickup extends ModItem {
    static BuffSeconds = 10;

    SetStaticDefaults() {
        ItemID.Sets.ItemsThatShouldNotBeInInventory[this.Type] = true;
        ItemID.Sets.IgnoresEncumberingStone[this.Type] = true;
        // Como coração e estrela: o jogo deixa pegar com o inventário cheio, e
        // o item cede o lugar quando o mundo tem itens demais no chão.
        ItemID.Sets.IsAPickup[this.Type] = true;
    }

    SetDefaults() {
        this.Item.width = 12;
        this.Item.height = 12;
    }

    // false: o item some sem entrar no inventário. O som da coleta também
    // fica com o mod (o jogo só toca o dele quando o item entra).
    OnPickup(worldItem, player) {
        player.AddBuff(ModContent.BuffType('ExampleDefenseBuff'), ExamplePickup.BuffSeconds * 60, false);
        SoundEngine.PlaySound(SoundID.Grab, player.Center);
        return false;
    }

    // Com o IsAPickup, não precisa de ItemSpace para pegar com o inventário
    // cheio. E, como os corações, ele não usa o CanPickup para ficar no chão
    // quando o jogador já tem o buff.

    // De mais longe com a Poção de Alcance Cardíaco (player.lifeMagnet), como
    // os corações. grabRange é um Ref, em pixels.
    GrabRange(worldItem, player, grabRange) {
        if (player.lifeMagnet) grabRange.value += 250;
    }
}
