const { ItemID } = Terraria.ID;

export class ExampleLastPrism extends ModItem {
    static get OverrideColor() { return Color.new(122, 173, 255); }

    get Texture() { return 'Content/Items/Weapons/Magic/ExampleLaserWeapon'; }

    SetDefaults() {
        this.CloneDefaults(ItemID.LastPrism);
        this.Item.mana = 4;
        this.Item.damage = 42;
        this.Item.shoot = ModContent.ProjectileType('ExampleLastPrismHoldout');
        this.Item.shootSpeed = 30;
        this.Item.color = ExampleLastPrism.OverrideColor;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }

    CanUseItem(item, player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType('ExampleLastPrismHoldout')] <= 0;
    }
}
