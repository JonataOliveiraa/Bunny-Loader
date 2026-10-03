import { ExampleDamageClass } from '../../DamageClasses/ExampleDamageClass.js';

const { ItemUseStyleID, SoundID } = Terraria.ID;

// Uma espada com a classe de dano de exemplo.
export class ExampleCustomDamageWeapon extends ModItem {
    get Texture() { return 'Content/Items/Weapons/Melee/ExampleMeleeWeapon'; }

    SetDefaults() {
        this.Item.DamageType = ModContent.GetInstance(ExampleDamageClass);
        this.Item.width = 40;
        this.Item.height = 40;
        this.Item.useStyle = ItemUseStyleID.Swing;
        this.Item.useTime = 30;
        this.Item.useAnimation = 30;
        this.Item.autoReuse = true;
        this.Item.damage = 70;
        this.Item.knockBack = 4;
        this.Item.crit = 6;
        this.Item.value = Terraria.Item.buyPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = SoundID.Item1;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModContent.ItemType('ExampleItem'), 20)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
