using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleRocketLauncher : ModItem
{
    public override void SetStaticDefaults()
    {
        AmmoID.Sets.SpecificLauncherAmmoProjectileFallback[Type] = ItemID.RocketLauncher;

        AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.Add(Type, new Dictionary<int, int> {
            { ItemID.RocketIII, ProjectileID.Meowmere },
        });
    }

    public override void SetDefaults()
    {
        Item.DefaultToRangedWeapon(ProjectileID.RocketI, AmmoID.Rocket, singleShotTime: 30, shotVelocity: 5f, hasAutoReuse: true);
        Item.width = 50;
        Item.height = 20;
        Item.damage = 55;
        Item.knockBack = 4f;
        Item.UseSound = SoundID.Item11;
        Item.value = Item.buyPrice(gold: 40);
        Item.rare = ItemRarityID.Yellow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-8f, 2f);
}
