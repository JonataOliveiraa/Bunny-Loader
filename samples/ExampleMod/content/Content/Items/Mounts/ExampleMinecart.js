import { ExampleMinecartMount } from '../../Mounts/ExampleMinecartMount.js';

const { ItemID, TileID } = Terraria.ID;

// O carrinho de mina (fica no espaço de carrinho do equipamento).
export class ExampleMinecart extends ModItem {
    SetDefaults() {
        this.Item.mountType = ModContent.MountType(ExampleMinecartMount);
        this.Item.width = 34;
        this.Item.height = 22;
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Blue;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.Minecart)
            .AddIngredient(ModContent.ItemType('ExampleItem'), 15)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
