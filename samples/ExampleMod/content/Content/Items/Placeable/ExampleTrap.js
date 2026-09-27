const { ItemID } = Terraria.ID;

// As duas armadilhas de exemplo: o mesmo tile, estilos 0 (bala de ícor) e 1
// (bala de clorofita). O tModLoader faz as duas de uma classe (ICustomAutoload);
// aqui são duas classes pequenas.
class ExampleTrapBase extends ModItem {
    get PlaceStyle() { return 0; }

    SetDefaults() {
        this.DefaultToPlaceableTile(ModContent.TileType('ExampleTrap'), this.PlaceStyle);
        this.Item.width = 12;
        this.Item.height = 12;
        this.Item.value = 10000;
        this.Item.mech = true;   // mostra os fios na mão
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ItemID.DartTrap)
            .Register();
    }
}

export class ExampleTrapIchorBullet extends ExampleTrapBase {}

export class ExampleTrapChlorophyteBullet extends ExampleTrapBase {
    get PlaceStyle() { return 1; }
}
