const { ItemID, DustID } = Terraria.ID;
const Main = Terraria.Main;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

// Código para todas as receitas, as do jogo também. O tModLoader 1.4 não tem
// mais esta classe (cada receita tem o AddCondition e os callbacks, ver
// ExampleRecipes); o Bunny Loader mantém os três ganchos dela.
export class ExampleGlobalRecipe extends GlobalRecipe {
    // Na Lua de Sangue, a Espada de Madeira some do menu de criação.
    RecipeAvailable(recipe) {
        return !(Main.bloodMoon && recipe.createItem.type === ItemID.WoodenSword);
    }

    // Com a Defesa de Exemplo, as receitas gastam metade da madeira.
    ConsumeItem(recipe, type, amount) {
        const player = Main.player[Main.myPlayer];
        if (type === ItemID.Wood && player.FindBuffIndex(ModContent.BuffType('ExampleDefenseBuff')) >= 0) {
            amount.value = Math.ceil(amount.value / 2);
        }
    }

    // O que sai da Bancada de Exemplo nasce com faíscas em volta do jogador.
    OnCraft(item, recipe) {
        if (recipe.requiredTile !== ModContent.TileType('ExampleWorkbench')) return;

        const player = Main.player[Main.myPlayer];
        for (let k = 0; k < 12; k++) {
            NewDust(player.position, player.width, player.height, DustID.GoldFlame, Rand.NextFloat(-2, 2), Rand.NextFloat(-3, -1), 0, Color.White, 1.2);
        }
    }
}
