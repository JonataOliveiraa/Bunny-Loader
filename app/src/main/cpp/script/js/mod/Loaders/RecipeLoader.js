class RecipeLoader {
    // O `Recipe.maxRecipes` do jogo: os laços dele param nele. Lido do jogo, e
    // não fixo: subiu de 3600 para 3700 na 1.4.5.8.6, e com o número velho as
    // receitas de 3600 a 3699 entravam duas vezes no menu de criação.
    static #max = 0;
    static get MAX_RECIPES() {
        return RecipeLoader.#max || (RecipeLoader.#max = Terraria.Recipe.maxRecipes);
    }

    static #added = 0;
    static #beyond = 0;
    static #groups = new Map();
    static #stations = new Set();   // os tiles de mod pedidos como estação

    static Added(index, station = -1) {
        RecipeLoader.#added++;
        if (TileLoader.ByType.has(station)) RecipeLoader.#stations.add(station);

        if (index >= RecipeLoader.MAX_RECIPES) {
            RecipeLoader.#beyond++;
            Hooks.Once('recipe.beyondMax', () => RecipeLoader.#HookBeyondMax());
        }
    }

    static Group(name) { return RecipeLoader.#groups.get(name); }
    static SetGroup(name, group) { RecipeLoader.#groups.set(name, group); }

    // As tabelas que o jogo monta a partir das receitas, depois de todas.
    static Finish() {
        if (!RecipeLoader.#added) return;

        const steps = [
            () => Terraria.Recipe['void CreateRequiredItemQuickLookups()'](),
            () => Terraria.Recipe['void UpdateMaterialFieldForAllRecipes()'](),
            () => Terraria.Recipe.UpdateWhichItemsAreMaterials(),
            () => Terraria.Recipe.UpdateWhichItemsAreCrafted(),
            () => Terraria.GameContent.ShimmerTransforms.UpdateRecipeSets(),
            () => Terraria.ID.ContentSamples.FixItemsAfterRecipesAreAdded(),
            () => TileLoader.SetCraftingStations(RecipeLoader.#stations),
        ];
        for (const step of steps) Safe.Run('receitas', step);

        bl.log('receitas de mod: ' + RecipeLoader.#added + ' (total ' + Terraria.Recipe.numRecipes + ')');
        if (RecipeLoader.#beyond) {
            bl.log('receitas de mod: ' + RecipeLoader.#beyond + ' alem da posicao ' + RecipeLoader.MAX_RECIPES +
                   ', percorridas pelos hooks do menu de criacao e do Guia');
        }
    }

    // Como o Recipe Limit Fix do TL Pro: o jogo percorre até o maxRecipes, e o
    // resto da tabela passa por aqui depois dele.
    static #HookBeyondMax() {
        const Recipe = Terraria.Recipe;

        Recipe['void FindRecipes(bool canDelayCheck)'].hook((original, canDelayCheck) => {
            if (canDelayCheck || Recipe.numRecipes <= RecipeLoader.MAX_RECIPES) {
                original(canDelayCheck);
                return;
            }

            let oldRecipe = 0;
            Safe.Run('receitas alem do limite', () => {
                RecipeLoader.#GrowAvailable(Recipe.numRecipes);
                oldRecipe = Terraria.Main.availableRecipe[Terraria.Main.focusRecipe];
            });
            original(false);

            Safe.Run('receitas alem do limite', () => RecipeLoader.#FindBeyondMax(oldRecipe));
        });

        bl.classOf('', 'GUICraftGuidePopup')['void FindRecipes()'].hook((original, self) => {
            original(self);
            if (Recipe.numRecipes <= RecipeLoader.MAX_RECIPES) return;

            Safe.Run('Guia alem do limite', () => RecipeLoader.#FindGuideBeyondMax(self));
        });

        Recipe.UpdateItemVariants.hook((original) => {
            original();
            if (Recipe.numRecipes <= RecipeLoader.MAX_RECIPES) return;

            Safe.Run('variantes alem do limite', () => RecipeLoader.#RefreshBeyondMax());
        });
    }

    // availableRecipe (do LocalUserGameState) e availableRecipeY nascem com o maxRecipes.
    static #GrowAvailable(total) {
        const Main = Terraria.Main;

        const available = Main.availableRecipe;
        if (available && available.length < total) Main.availableRecipe = available.cloneResized(total);

        const positions = Main.availableRecipeY;
        if (positions && positions.length < total) Main.availableRecipeY = positions.cloneResized(total);
    }

    static #FindBeyondMax(oldRecipe) {
        const Main = Terraria.Main;
        const Recipe = Terraria.Recipe;
        const recipes = Main.recipe;
        const total = Recipe.numRecipes;

        const player = Main.LocalPlayer;
        Recipe.CollectItemsToCraftWithFrom(player);

        for (let i = RecipeLoader.MAX_RECIPES; i < total; i++) {
            const recipe = recipes[i];
            if (!recipe || recipe.createItem.type === 0) break;

            if (Recipe.PlayerMeetsTileRequirements(player, recipe) &&
                Recipe.PlayerMeetsEnvironmentConditions(player, recipe) &&
                Recipe['bool CollectedEnoughItemsToCraft(Recipe recipe)'](recipe)) {
                Recipe.AddToAvailableRecipes(i);
            }
        }

        Recipe.TryRefocusingRecipe(oldRecipe);
    }

    // O mesmo filtro do jogo: item do Guia com tipo e pilha, e um ingrediente que o aceite.
    static #FindGuideBeyondMax(popup) {
        const Recipe = Terraria.Recipe;
        const total = Recipe.numRecipes;

        const guide = bl.classOf('', 'GUIInstance').Active?.GUICraftGuide;
        const item = guide ? guide.guideItem : null;
        if (!item || item.type < 1 || item.stack < 1) return;

        if (popup.availableGuideRecipe.length < total) {
            popup.availableGuideRecipe = popup.availableGuideRecipe.cloneResized(total);
        }

        const type = item.type;
        const recipes = Terraria.Main.recipe;
        const found = popup.availableGuideRecipe;
        for (let i = RecipeLoader.MAX_RECIPES; i < total; i++) {
            const recipe = recipes[i];
            if (!recipe || recipe.createItem.type === 0) break;

            if (RecipeLoader.#UsesItem(recipe, type)) found[popup.numAvailableGuideRecipes++] = i;
        }
    }

    static #UsesItem(recipe, type) {
        const lookup = recipe.requiredItemQuickLookup;

        for (let j = 0; j < ModRecipe.MaxIngredients; j++) {
            const entry = lookup[j];
            if (entry.itemIdOrRecipeGroup === 0) return false;
            if (entry.Matches(type)) return true;
        }
        return false;
    }

    static #RefreshBeyondMax() {
        const recipes = Terraria.Main.recipe;
        const total = Terraria.Recipe.numRecipes;

        for (let i = RecipeLoader.MAX_RECIPES; i < total; i++) {
            const recipe = recipes[i];
            if (!recipe) break;

            recipe.createItem.Refresh(true);
            const ingredients = recipe.requiredItem;
            for (let j = 0; j < ingredients.length && ingredients[j].type !== 0; j++) ingredients[j].Refresh(true);
        }
    }
}
