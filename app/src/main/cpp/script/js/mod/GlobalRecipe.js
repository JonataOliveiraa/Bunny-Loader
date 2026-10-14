// Código de mod para TODA receita (as do jogo e as de mod). O tModLoader 1.4
// trocou o GlobalRecipe pelo AddCondition/AddConsumeIngredientCallback/
// AddOnCraftCallback de cada receita (que o ModRecipe também tem); aqui ficam
// os três ganchos dele que valem para todas.
class GlobalRecipe {
    SetStaticDefaults() {}
    PostSetupContent() {}

    // false: a receita sai do menu de criação (o Guia continua mostrando).
    RecipeAvailable(recipe) { return true; }

    // Quanto sai de um ingrediente a cada criação: amount é Ref, já com o
    // desconto da Mesa de Alquimia. type é o item (o do ícone, num grupo).
    ConsumeItem(recipe, type, amount) {}

    // O item criado, antes de ir para o cursor ou o inventário.
    OnCraft(item, recipe) {}

    static register(cls) {
        const inst = globalRecipes.Register(cls);
        RecipeLoader.HookGlobal(cls);
        return inst;
    }
}
