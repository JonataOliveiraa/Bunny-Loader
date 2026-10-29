using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleMinigun : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToRangedWeapon(ProjectileID.PurificationPowder, AmmoID.Bullet, 5, 16f, true);
        Item.SetWeaponValues(11, 1f);

        Item.width = 54;
        Item.height = 22;
        Item.rare = ItemRarityID.Green;
        Item.UseSound = SoundID.Item11;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }

    public override bool CanConsumeAmmo(Item ammo, Player player)
    {
        return Main.rand.NextFloat() >= 0.38f;
    }

    public override bool NeedsAmmo(Player player)
    {
        return player.CountItem(ModContent.ItemType<ExampleItem>(), 10) < 10;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(10));
    }

    public override Vector2? HoldoutOffset()
    {
        return new Vector2(-6f, -2f);
    }
}
