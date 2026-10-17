import { ExampleStatIncreasePlayer } from '../../../Common/Players/ExampleStatIncreasePlayer.js';

const { ItemID } = Terraria.ID;
const Main = Terraria.Main;

// Mais vida depois das frutas da vida do jogo: +10 por fruta, até 10. Os
// corações com ela ganham o desenho do VanillaLifeOverlay.
export class ExampleLifeFruit extends ModItem {
    static MaxExampleLifeFruits = 10;
    static LifePerFruit = 10;

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', ExampleLifeFruit.LifePerFruit).replace('{1}', ExampleLifeFruit.MaxExampleLifeFruits);
        }
    }

    SetStaticDefaults() {
        this.Item.ResearchUnlockCount = 10;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.LifeFruit);
    }

    // Só com as frutas da vida do jogo no máximo (500 de vida de base).
    CanUseItem(item, player) {
        return player.statLifeMax >= 500;
    }

    UseItem(item, player) {
        const stats = player.GetModPlayer(ExampleStatIncreasePlayer);
        if (stats.exampleLifeFruits >= ExampleLifeFruit.MaxExampleLifeFruits) return null;
        stats.exampleLifeFruits++;
        player.statLife += ExampleLifeFruit.LifePerFruit;
        if (Main.myPlayer === player.whoAmI) player.HealEffect(ExampleLifeFruit.LifePerFruit, true);
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
