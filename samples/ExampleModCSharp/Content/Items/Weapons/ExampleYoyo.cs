using System;
using ExampleMod.Content.Projectiles;
using ExampleMod.Content.Rarities;
using ExampleMod.Content.Tiles.Furniture;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleYoyo : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.Yoyo[Type] = true;
        ItemID.Sets.GamepadExtraRange[Type] = 15;
        ItemID.Sets.GamepadSmartQuickReach[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 24;

        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useTime = 25;
        Item.useAnimation = 25;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.UseSound = SoundID.Item1;

        Item.damage = 40;
        Item.DamageType = DamageClass.MeleeNoSpeed;
        Item.knockBack = 2.5f;
        Item.crit = 8;
        Item.channel = true;
        Item.rare = ModContent.RarityType<ExampleModRarity>();
        Item.value = Item.buyPrice(gold: 1);

        Item.shoot = ModContent.ProjectileType<ExampleYoyoProjectile>();
        Item.shootSpeed = 16f;
    }

    private static readonly int[] unwantedPrefixes = [PrefixID.Terrible, PrefixID.Dull, PrefixID.Shameful, PrefixID.Annoying, PrefixID.Broken, PrefixID.Damaged, PrefixID.Shoddy];

    public override bool AllowPrefix(int pre) => Array.IndexOf(unwantedPrefixes, pre) == -1;

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<ExampleWorkbench>()
            .Register();
    }
}
