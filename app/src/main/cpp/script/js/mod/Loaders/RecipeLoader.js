class RecipeLoader {
    // O menu de criação do celular só percorre as receitas até esta posição.
    static VISIBLE = 3600;

    static #added = 0;
    static #hidden = 0;
    static #groups = new Map();

    static Added(index) {
        RecipeLoader.#added++;
        if (index >= RecipeLoader.VISIBLE) RecipeLoader.#hidden++;
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
        ];
        for (const step of steps) Safe.Run('receitas', step);

        bl.log('receitas de mod: ' + RecipeLoader.#added + ' (total ' + Terraria.Recipe.numRecipes + ')');
        if (RecipeLoader.#hidden) {
            bl.log('receitas de mod: ' + RecipeLoader.#hidden + ' alem da posicao ' + RecipeLoader.VISIBLE +
                   ' nao aparecem no menu de criacao (limite do jogo)');
        }
    }
}
