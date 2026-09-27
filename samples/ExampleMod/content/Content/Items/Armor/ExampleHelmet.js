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

    IsArmorSet(head, body, legs) {
        return body.type === ModContent.ItemType('ExampleBreastplate') && legs.type === ModContent.ItemType('ExampleLeggings');
    }

    // O setBonus é a linha "Bônus do conjunto" no tooltip das peças vestidas.
    UpdateArmorSet(item, player) {
        const bonus = ExampleHelmet.AdditiveGenericDamageBonus;
        player.setBonus = ModLocalization.Translate('ArmorSetBonus.ExampleArmor').replace('{0}', bonus);

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
