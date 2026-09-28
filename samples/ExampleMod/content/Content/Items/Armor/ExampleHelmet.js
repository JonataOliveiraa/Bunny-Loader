// O capacete do conjunto: com o peitoral e as calças de exemplo, mais dano.
// ExampleHelmet_Head.png é a textura na cabeça (o EquipLoader acha pelo nome).
export class ExampleHelmet extends ModItem {
    static AdditiveGenericDamageBonus = 20;

    SetDefaults() {
        this.Item.width = 18;
        this.Item.height = 18;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.defense = 5;
    }

    // O conjunto do 1.4.5: o jogo mostra "Bônus definido" no tooltip de cada
    // peça (com "(2/3)" vestida) e chama o UpdateArmorSet quando está completo.
    AddArmorSets() {
        const text = ModLocalization.Translate('ArmorSetBonus.ExampleArmor').replace('{0}', ExampleHelmet.AdditiveGenericDamageBonus);
        this.CreateArmorSet(this.Type, ModContent.ItemType('ExampleBreastplate'), ModContent.ItemType('ExampleLeggings'), text);
    }

    UpdateArmorSet(item, player) {
        const bonus = ExampleHelmet.AdditiveGenericDamageBonus;
        player.meleeDamage += bonus / 100;
        player.rangedDamage += bonus / 100;
        player.magicDamage += bonus / 100;
        player.minionDamage += bonus / 100;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
