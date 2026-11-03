using ExampleMod.Content.Tiles.Furniture;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleMultiplePrefixCategoryWeapon : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.autoReuse = true;
        Item.damage = 70;
        Item.knockBack = 4;
        Item.crit = 6;
        Item.mana = 6;
        Item.value = Item.buyPrice(gold: 1);
        Item.rare = ItemRarityID.Green;
        Item.UseSound = SoundID.Item1;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(20)
            .AddTile<ExampleWorkbench>()
            .Register();
    }

    public override bool MeleePrefix() => true;

    public override bool MagicPrefix() => true;

    public override bool RangedPrefix() => false;
}
