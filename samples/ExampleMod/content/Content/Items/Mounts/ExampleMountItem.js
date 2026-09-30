import { ExampleMount } from '../../Mounts/ExampleMount.js';

const { ItemUseStyleID, SoundID } = Terraria.ID;

// O item que invoca o carro (fica no espaço de montaria do equipamento).
export class ExampleMountItem extends ModItem {
    SetDefaults() {
        this.Item.width = 20;
        this.Item.height = 30;
        this.Item.useTime = 20;
        this.Item.useAnimation = 20;
        this.Item.useStyle = ItemUseStyleID.Swing;
        this.Item.value = Terraria.Item.sellPrice(0, 3, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = SoundID.Item79;
        this.Item.noMelee = true;
        this.Item.mountType = ModContent.MountType(ExampleMount);
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
