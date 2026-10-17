import { ExampleStatIncreasePlayer } from '../../../Common/Players/ExampleStatIncreasePlayer.js';

const { ItemID } = Terraria.ID;
const Main = Terraria.Main;

// Mais mana depois dos cristais de mana do jogo: +10 por cristal, até 10. As
// estrelas com ele ganham o desenho do VanillaManaOverlay.
export class ExampleManaCrystal extends ModItem {
    static MaxExampleManaCrystals = 10;
    static ManaPerCrystal = 10;

    ModifyTooltipLines() {
        for (let i = 0; i < this.TooltipLines.length; i++) {
            this.TooltipLines[i] = this.TooltipLines[i].replace('{0}', ExampleManaCrystal.ManaPerCrystal).replace('{1}', ExampleManaCrystal.MaxExampleManaCrystals);
        }
    }

    SetStaticDefaults() {
        this.Item.ResearchUnlockCount = 10;
    }

    SetDefaults() {
        this.CloneDefaults(ItemID.ManaCrystal);
    }

    // Só com os cristais do jogo no máximo (200 de mana de base).
    CanUseItem(item, player) {
        return player.statManaMax >= 200;
    }

    UseItem(item, player) {
        const stats = player.GetModPlayer(ExampleStatIncreasePlayer);
        if (stats.exampleManaCrystals >= ExampleManaCrystal.MaxExampleManaCrystals) return null;
        stats.exampleManaCrystals++;
        player.statMana += ExampleManaCrystal.ManaPerCrystal;
        if (Main.myPlayer === player.whoAmI) player.ManaEffect(ExampleManaCrystal.ManaPerCrystal);
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
