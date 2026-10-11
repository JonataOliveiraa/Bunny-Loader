const { ItemUseStyleID, SoundID } = Terraria.ID;

// Espada curta: quem acerta é o projétil (ExampleShortswordProjectile), que o
// jogador segura e empurra para a frente. O item só some da mão e o dispara.
export class ExampleShortsword extends ModItem {
    SetDefaults() {
        this.Item.damage = 8;
        this.Item.knockBack = 4;
        this.Item.useStyle = ItemUseStyleID.Rapier;   // o movimento do braço das espadas curtas
        this.Item.useAnimation = 12;
        this.Item.useTime = 12;
        this.Item.width = 32;
        this.Item.height = 32;
        this.Item.UseSound = SoundID.Item1;
        this.Item.DamageType = DamageClass.MeleeNoSpeed;
        this.Item.autoReuse = false;
        this.Item.noUseGraphic = true;   // o que aparece é o projétil, não o item
        this.Item.noMelee = true;        // o dano é do projétil, não do item

        this.Item.rare = ItemRarityID.White;
        this.Item.value = Terraria.Item.sellPrice(0, 0, 0, 10);

        this.Item.shoot = ModProjectile.getTypeByName('ExampleShortswordProjectile');
        // Vira a velocidade do projétil: quanto ele avança a cada atualização.
        this.Item.shootSpeed = 2.1;
    }

    // Com noUseGraphic, o jogo não a vê como arma corpo a corpo para os
    // prefixos. Assim ela ganha os de espada.
    MeleePrefix() {
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'))
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
