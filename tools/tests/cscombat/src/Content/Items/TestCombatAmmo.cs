using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsCombat.Content.Items;

public class TestCombatAmmo : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;
        Item.width = 8;
        Item.height = 8;
        Item.damage = 12;
        Item.knockBack = 1.5f;
        Item.maxStack = 9999;
        Item.consumable = true;
        Item.shoot = ProjectileID.Bullet;
        Item.shootSpeed = 4.5f;
        Item.ammo = AmmoID.Bullet;
    }

    public override void PickAmmo(Item weapon, Player player, ref int type, ref float speed, ref StatModifier damage, ref float knockback)
    {
        Checks.PickCalls++;
        speed += 1;
        damage.Flat += 1000;
    }

    public override bool? CanBeChosenAsAmmo(Item weapon, Player player) => Checks.RefuseAmmo ? false : null;

    public override void OnConsumedAsAmmo(Item weapon, Player player) => Checks.ConsumedCalls++;
}
