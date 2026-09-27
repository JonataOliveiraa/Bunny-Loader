export class ExampleBeard extends ModItem {
    // A barba pinta com a cor do cabelo do jogador.
    SetStaticDefaults() {
        Terraria.ID.ArmorIDs.Beard.Sets.UseHairColor[this.Item.beardSlot] = true;
    }

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 14;
        this.Item.maxStack = 1;
        this.Item.color = Terraria.Main.LocalPlayer.hairColor;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.accessory = true;
        this.Item.vanity = true;
    }
}
