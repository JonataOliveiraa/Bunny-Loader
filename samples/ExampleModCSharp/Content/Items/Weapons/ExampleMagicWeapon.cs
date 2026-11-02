using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleMagicWeapon : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToStaff(ProjectileID.BlackBolt, 7, 20, 11);
        Item.width = 34;
        Item.height = 40;
        Item.UseSound = SoundID.Item71;

        Item.SetWeaponValues(25, 6, 32);

        Item.SetShopValues(ItemRarityColor.LightRed4, 10000);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }

    public override void ModifyManaCost(Player player, ref float reduce, ref float mult)
    {
        if (player.statLife < player.statLifeMax2 / 2)
        {
            mult *= 0.5f;
        }
    }
}
