using ExampleMod.Content.Tiles.Furniture;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

// This weapon has two attacks: a normal use and an alternate use (right click on PC, the alternate attack button on the phone).
// The normal use is a slow, strong swing that sets enemies on fire. The alternate use is a faster thrust that inflicts Ichor and shoots bees.
public class ExampleDualUseWeapon : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.autoReuse = true;

        Item.DamageType = DamageClass.Melee;
        Item.damage = 50;
        Item.knockBack = 6;

        Item.value = 10000;
        Item.rare = ItemRarityID.Green;
        Item.UseSound = SoundID.Item1;

        Item.shoot = ProjectileID.Bee;
        Item.shootSpeed = 5f;
    }

    // Returning true here allows the alternate use. While it is being used, player.altFunctionUse is 2.
    public override bool AltFunctionUse(Player player)
    {
        return true;
    }

    // CanUseItem runs before every use, so it's a good place to switch the item's stats between the two attacks.
    // Every stat changed in one branch must be set in the other as well, otherwise it would keep the value from the last use.
    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse == 2)
        {
            Item.useStyle = ItemUseStyleID.Thrust;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.damage = 50;
            Item.shoot = ProjectileID.Bee;
        }
        else
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.damage = 100;
            Item.shoot = ProjectileID.None; // The normal swing doesn't shoot anything.
        }
        return base.CanUseItem(player);
    }

    // Each attack inflicts a different debuff, for 1 second (60 ticks).
    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (player.altFunctionUse == 2)
        {
            target.AddBuff(BuffID.Ichor, 60);
        }
        else
        {
            target.AddBuff(BuffID.OnFire, 60);
        }
    }

    // Emits dusts matching the debuff of the current attack: Ichor drops for the thrust, fire for the swing.
    public override void MeleeEffects(Player player, Rectangle hitbox)
    {
        if (!Main.rand.NextBool(3))
        {
            return;
        }
        if (player.altFunctionUse == 2)
        {
            Dust dust = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.IchorTorch, 0f, 0f, 100, default, 2f);
            dust.noGravity = true;
            dust.velocity.X += player.direction * 2f;
            dust.velocity.Y += 0.2f;
        }
        else
        {
            Dust dust = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Torch, player.velocity.X * 0.2f + player.direction * 3, player.velocity.Y * 0.2f, 100, default, 2.5f);
            dust.noGravity = true;
        }
    }

    // Only the thrust shoots. The bees fly forwards in a 30 degree spread and deal 10% of the weapon's damage.
    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        velocity = new Vector2(velocity.Length() * (velocity.X > 0 ? 1 : -1), 0f).RotatedByRandom(MathHelper.ToRadians(30));
        damage = (int)(damage * 0.1f);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile<ExampleWorkbench>()
            .Register();
    }
}
