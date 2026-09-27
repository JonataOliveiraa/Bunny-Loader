// As tarefas de "conteúdo pronto", por fase. 'groups' roda antes de tudo (uma
// receita pode usar o grupo de outro mod); 'mods' antes do conteúdo, como no tModLoader.
class Ready {
    static #tasks = { groups: [], mods: [], content: [] };
    static #hooked = false;

    static Add(task, phase = 'content') {
        Ready.#tasks[phase].push(task);
        if (Ready.#hooked) return;

        Ready.#hooked = true;
        bl.onContentReady(() => {
            for (const t of Ready.#tasks.groups) Safe.Run('AddRecipeGroups', t);
            for (const t of Ready.#tasks.mods) Safe.Run('Mod.PostSetupContent', t);
            for (const t of Ready.#tasks.content) Safe.Run('PostSetupContent', t);

            BestiaryLoader.Finish();
            RecipeLoader.Finish();
        });
    }
}
