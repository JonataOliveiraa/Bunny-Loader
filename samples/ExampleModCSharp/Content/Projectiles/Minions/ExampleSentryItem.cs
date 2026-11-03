using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles.Minions;

public class ExampleSentryItem : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
        ItemID.Sets.LockOnIgnoresCollision[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.damage = 50;
        Item.DamageType = DamageClass.Summon;
        Item.sentry = true;
        Item.mana = 10;
        Item.width = 26;
        Item.height = 28;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.noMelee = true;
        Item.knockBack = 3;
        Item.value = Item.buyPrice(gold: 30);
        Item.rare = ItemRarityID.Cyan;
        Item.UseSound = SoundID.Item83;
        Item.shoot = ModContent.ProjectileType<ExampleSentry>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        bool canPlaceInAir = false;

        if (player.direction == 1)
        {
            canPlaceInAir = true;
        }

        position = Main.MouseWorld;
        player.LimitPointToPlayerReachableArea(ref position);
        int halfProjectileHeight = (int)Math.Ceiling(ContentSamples.ProjectilesByType[type].height / 2f);

        if (!canPlaceInAir)
        {
            player.FindSentryRestingSpot(type, out int worldX, out int worldY, out int pushYUp);
            position = new Vector2(worldX, worldY - halfProjectileHeight);
        }
        else
        {
            position.Y -= halfProjectileHeight;
        }

        Projectile.NewProjectile(source, position, Vector2.Zero, type, damage, knockback, Main.myPlayer, ai2: canPlaceInAir ? 0 : 1);
        player.UpdateMaxTurrets();

        return false;
    }
}
