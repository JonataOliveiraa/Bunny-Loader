import { ExampleCustomDrawDust } from '../../Dusts/ExampleCustomDrawDust.js';

const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExamplePickaxe extends ModItem {
    SetDefaults() {
        this.Item.melee = true;
        this.Item.pick = 220;
        this.SetWeaponValues(20, 6, 0);
        this.SetDefaultWeaponStyle(10, true);
        this.Item.value = Terraria.Item.sellPrice(0, 1, 0, 0);
        this.Item.rare = ItemRarityID.Green;
        this.Item.UseSound = Terraria.ID.SoundID.Item1;
    }

    // A poeira com rastro (ExampleCustomDrawDust) no golpe.
    MeleeEffects(item, player, hitbox) {
        if (Rand.NextBool(10)) {
            NewDust(Vector2.new(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, ModContent.DustType(ExampleCustomDrawDust), 0, 0, 0, Color.White, 1);
        }
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
