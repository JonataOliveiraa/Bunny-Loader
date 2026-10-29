using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCombat.Content.Items;

public class TestCombatGun : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;
        Item.width = 54;
        Item.height = 22;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useTime = 10;
        Item.useAnimation = 10;
        Item.damage = 10;
        Item.knockBack = 1;
        Item.noMelee = true;
        Item.shoot = ProjectileID.PurificationPowder;
        Item.shootSpeed = 1f;
        Item.useAmmo = AmmoID.Bullet;
    }

    public override bool CanShoot(Player player) => !Checks.BlockShoot;

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        Checks.ModifyShootCalls++;
        Checks.SpeedIn = velocity.Length();
        velocity *= 2;
        damage += 5;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Checks.ShootCalls++;
        Checks.SpeedOut = velocity.Length();
        Checks.ShotAmmo = source.IsNull ? -1 : source.AmmoItemIdUsed;
        if (Checks.OwnShot)
        {
            Checks.OwnIndex = Projectile.NewProjectile(source, position, velocity, ProjectileID.WoodenArrowFriendly, damage, knockback, player.whoAmI);
            return false;
        }
        return !Checks.SkipShot;
    }

    public override bool NeedsAmmo(Player player) => !Checks.NoAmmo;

    public override bool CanConsumeAmmo(Item ammo, Player player) => !Checks.KeepAmmo;

    public override void OnConsumeAmmo(Item ammo, Player player) => Checks.ConsumeCalls++;
}
