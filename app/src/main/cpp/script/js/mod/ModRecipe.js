// Só vale do AddRecipes em diante: antes disso o jogo não montou as receitas dele.
class ModRecipe {
    static MaxIngredients = 15;

    constructor() {
        this.recipe = Terraria.Recipe.currentRecipe;
        this.ingredients = 0;
        this.craftingStation = -1;
        this.customShimmerResults = [];
        this.conditions = [];
        this.consumeCallbacks = [];
        this.craftCallbacks = [];
    }

    SetResult(type, stack = 1) {
        const item = this.recipe.createItem;
        item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        item.stack = ModRecipe.#ClampStack(item, stack);
        return this;
    }

    AddIngredient(type, stack = 1) {
        if (this.ingredients >= ModRecipe.MaxIngredients) {
            bl.log('ModRecipe: mais de ' + ModRecipe.MaxIngredients + ' ingredientes; ' + type + ' ficou de fora');
            return this;
        }

        const item = this.recipe.requiredItem[this.ingredients++];
        item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        item.stack = ModRecipe.#ClampStack(item, stack);
        Terraria.ID.ItemID.Sets.IsAMaterial[type] = true;
        return this;
    }

    // Sem ingrediente do grupo ainda, põe o item-modelo dele com `stack`.
    AddRecipeGroup(group, stack = 1) {
        const g = ModRecipe.GetGroup(group);
        if (!g) {
            bl.log('ModRecipe: grupo de receita "' + group + '" nao existe');
            return this;
        }

        let covered = false;
        for (let i = 0; i < this.ingredients && !covered; i++) {
            covered = g.Contains(this.recipe.requiredItem[i].type);
        }
        if (!covered) this.AddIngredient(g.GetPlaceholderItemType(), stack);

        this.recipe['void RequireGroup(RecipeGroup group)'](g);
        return this;
    }

    AddTile(tileType) {
        if (this.craftingStation !== -1) {
            bl.log('ModRecipe: a receita ja tem a estacao ' + this.craftingStation + '; ' + tileType + ' ficou de fora');
            return this;
        }

        this.recipe['void SetCraftingStation(int tileType)'](tileType);
        this.craftingStation = tileType;
        return this;
    }

    AddCustomShimmerResult(type, stack = 1) {
        if (type > 0) {
            const result = this.recipe['Item AddCustomShimmerResult(int itemType, int itemStack)'](type, stack);
            this.customShimmerResults.push(result);
        }
        return this;
    }

    // Uma Condition, ou (descrição, () => bool) como no tModLoader. Fora dela,
    // a receita sai do menu de criação.
    AddCondition(condition, predicate) {
        this.conditions.push(condition instanceof Condition ? condition : new Condition(condition, predicate));
        return this;
    }

    RemoveCondition(condition) {
        const i = this.conditions.indexOf(condition);
        if (i >= 0) this.conditions.splice(i, 1);
        return i >= 0;
    }

    // (recipe, type, amount, isDecrafting): amount é Ref, quanto sai do
    // ingrediente `type` a cada criação (0: não sai).
    AddConsumeIngredientCallback(callback) {
        if (typeof callback !== 'function') throw new TypeError('AddConsumeIngredientCallback(callback): pass a function');
        this.consumeCallbacks.push(callback);
        return this;
    }

    AddConsumeItemCallback(callback) { return this.AddConsumeIngredientCallback(callback); }

    // (recipe, item, consumedItems, destinationStack), antes de o item ir para
    // o cursor ou o inventário. O celular não dá consumedItems nem destinationStack (null).
    AddOnCraftCallback(callback) {
        if (typeof callback !== 'function') throw new TypeError('AddOnCraftCallback(callback): pass a function');
        this.craftCallbacks.push(callback);
        return this;
    }

    SetProperty(name, value) {
        this.recipe[name] = value;
        return this;
    }

    Register() {
        const Main = Terraria.Main;
        const index = Terraria.Recipe.numRecipes;

        // A tabela cresce: a receita existe (decraft, Guia) mesmo fora do menu.
        if (index >= Main.recipe.length) Main.recipe = Main.recipe.cloneResized(index + 1);

        Terraria.Recipe['void AddRecipe()']();
        RecipeLoader.Added(index, this.craftingStation, this);
    }

    // Objeto, nome de um do jogo ('IronBar'), nome de um criado aqui, ou o número.
    static GetGroup(groupOrName) {
        if (groupOrName && typeof groupOrName === 'object') return groupOrName;

        if (typeof groupOrName === 'number') {
            const all = Terraria.RecipeGroup.recipeGroups;
            return all.ContainsKey(groupOrName) ? all.get_Item(groupOrName) : undefined;
        }

        const mine = RecipeLoader.Group(groupOrName);
        if (mine) return mine;

        try {
            return Terraria.ID.RecipeGroups[groupOrName] || undefined;
        } catch (e) {
            return undefined;
        }
    }

    static GetGroupByName(name) { return ModRecipe.GetGroup(name); }

    // 'Qualquer <nome>' no menu; o nome sai de RecipeGroups.<name> se houver.
    static CreateRecipeGroup(name, itemTypes = []) {
        const known = RecipeLoader.Group(name);
        if (known) return known;

        const key = ModLocalization.Key('RecipeGroups.' + name);
        const group = Terraria.RecipeGroup.new();
        group['void .ctor(string groupDescriptorKey, int[] validItems)'](key.startsWith('Mods.') ? key : name, itemTypes);
        for (const t of itemTypes) Terraria.ID.ItemID.Sets.IsAMaterial[t] = true;

        group.Register();
        Safe.Run('grupo de receita ' + name, () => group['void SortDecraftingEntries()']());
        RecipeLoader.SetGroup(name, group);
        return group;
    }

    static #ClampStack(item, stack) {
        return Math.max(1, Math.min(stack | 0, item.maxStack || 9999));
    }
}
