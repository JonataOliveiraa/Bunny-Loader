// As tarefas de "conteúdo pronto", por fase. 'groups' roda antes de tudo (uma
// receita pode usar o grupo de outro mod); 'mods' antes do conteúdo, como no tModLoader.
class Ready {
    static #tasks = { resize: [], setup: [], groups: [], mods: [], content: [], recipes: [], postRecipes: [], finish: [] };
    static #hooked = false;

    static Add(task, phase = 'content') {
        Ready.#tasks[phase].push(task);
        if (Ready.#hooked) return;

        Ready.#hooked = true;
        bl.onContentReady(() => {
            for (const t of Ready.#tasks.resize) Safe.Run('ResizeArrays', t);
            // 'setup': o que os outros pedem pronto (o MountData das montarias).
            for (const t of Ready.#tasks.setup) Safe.Run('SetupContent', t);
            for (const t of Ready.#tasks.groups) Safe.Run('AddRecipeGroups', t);
            for (const t of Ready.#tasks.mods) Safe.Run('Mod.PostSetupContent', t);
            for (const t of Ready.#tasks.content) Safe.Run('PostSetupContent', t);

            for (const t of Ready.#tasks.recipes) Safe.Run('AddRecipes', t);
            for (const t of Ready.#tasks.postRecipes) Safe.Run('PostAddRecipes', t);

            BestiaryLoader.Finish();
            RecipeLoader.Finish();
            for (const t of Ready.#tasks.finish) Safe.Run('PostSetupRecipes', t);
        });
    }
}
