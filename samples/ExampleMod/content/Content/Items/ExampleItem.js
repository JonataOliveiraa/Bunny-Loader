export class ExampleCustomCurrency {
    static #id = -1;

    static get CurrencyId() {
        if (ExampleCustomCurrency.#id < 0) {
            const { CustomCurrencyManager, CustomCurrencySingleCoin } = Terraria.GameContent.UI;
            const currency = CustomCurrencySingleCoin.new();
            currency['void .ctor(int coinItemID, long currencyCap)'](ModItem.getTypeByName('ExampleItem'), 999);
            currency.CurrencyTextKey = ModLocalization.Translate('CustomCurrency.ExampleItemCurrency');
            currency.CurrencyTextColor = Color.new(240, 100, 120);
            ExampleCustomCurrency.#id = CustomCurrencyManager.RegisterCurrency(currency);
        }
        return ExampleCustomCurrency.#id;
    }
}

export class ExampleItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/' + this.constructor.name;
    }

    SetDefaults() {
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }

    AddRecipes() {
        this.CreateRecipe(999)
            .AddIngredient(Terraria.ID.ItemID.DirtBlock, 10)
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
