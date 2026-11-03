using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleSwingingEnergySword : ModItem
{
    public override void SetDefaults()
    {
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = 20;
        Item.useTime = 20;
        Item.damage = 72;
        Item.knockBack = 4.5f;
        Item.width = 40;
        Item.height = 40;
        Item.scale = 1f;
        Item.UseSound = SoundID.Item1;
        Item.rare = ItemRarityID.Pink;
        Item.value = Item.buyPrice(gold: 23);
        Item.DamageType = DamageClass.Melee;
        Item.shoot = ModContent.ProjectileType<ExampleSwingingEnergySwordProjectile>();
        Item.noMelee = true;
        Item.shootsEveryUse = true;
        Item.autoReuse = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        float adjustedItemScale = player.GetAdjustedItemScale(Item);
        Projectile.NewProjectile(source, player.MountedCenter, new Vector2(player.direction, 0f), type, damage, knockback, player.whoAmI, player.direction * player.gravDir, player.itemAnimationMax, adjustedItemScale);
        NetMessage.SendData(MessageID.PlayerControls, number: player.whoAmI);

        return base.Shoot(player, source, position, velocity, type, damage, knockback);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(12)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}
