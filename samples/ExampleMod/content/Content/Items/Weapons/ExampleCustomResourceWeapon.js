import { ExampleResourcePlayer } from '../../../Common/Players/ExampleResourcePlayer.js';

const { ItemUseStyleID, ProjectileID, SoundID } = Terraria.ID;

// Uma arma que gasta o recurso de exemplo (e não mana). Segurando ela, a
// barra do recurso aparece (ExampleResourceBar).
export class ExampleCustomResourceWeapon extends ModItem {
    exampleResourceCost = 5;

    SetDefaults() {
        const item = this.Item;
        item.damage = 130;
        item.magic = true;
        item.width = 38;
        item.height = 38;
        item.useTime = 16;
        item.useAnimation = 16;
        item.useStyle = ItemUseStyleID.Shoot;
        item.noMelee = true;
        item.knockBack = 6;
        item.value = Terraria.Item.buyPrice(0, 15, 0, 0);
        item.rare = ItemRarityID.Pink;
        item.UseSound = SoundID.Item71;
        item.autoReuse = true;
        item.shoot = ProjectileID.VortexBeaterRocket;
        item.shootSpeed = 7;
        item.crit = 32;
    }

    ModifyTooltips(item, tooltips) {
        const text = ModLocalization.Translate('Items.ExampleCustomResourceWeapon.UsesXExampleResource').replace('{0}', this.exampleResourceCost);
        tooltips.push(new TooltipLine(this.Mod, 'ExampleResourceCost', text));
    }

    // Só atira com recurso suficiente.
    CanUseItem(item, player) {
        return player.GetModPlayer(ExampleResourcePlayer).exampleResourceCurrent >= this.exampleResourceCost;
    }

    UseItem(item, player) {
        player.GetModPlayer(ExampleResourcePlayer).exampleResourceCurrent -= this.exampleResourceCost;
        return true;
    }

    AddRecipes() {
        this.CreateRecipe()
            .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
            .AddTile(ModContent.TileType('ExampleWorkbench'))
            .Register();
    }
}
