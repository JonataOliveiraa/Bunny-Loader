using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleWhip : ModItem
{
    public static readonly int ExampleWhipTagDamage = 5;

    public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(ExampleWhipTagDamage);

    public override void SetStaticDefaults()
    {
        ItemID.Sets.UniqueTagEffects[Type] = new WhipTagEffect() { TagDamage = ExampleWhipTagDamage };
    }

    public override void SetDefaults()
    {
        Item.DefaultToWhip(ModContent.ProjectileType<ExampleWhipProjectile>(), 20, 2, 4);
        Item.rare = ItemRarityID.Green;
        Item.value = Item.buyPrice(gold: 1);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        float swingDirection = 0.6f + (0.4f * Main.rand.NextFloat());
        if (Main.rand.NextBool(3))
        {
            swingDirection *= -2.5f;
        }

        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, swingDirection);
        return false;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }

    public override bool MeleePrefix() => true;
}
