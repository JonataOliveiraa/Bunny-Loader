using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons.Melee;

// A basic melee weapon. It's the same as ExampleSword, but uses Item.SetWeaponValues to set the damage, knockback and critical strike chance in one line.
public class ExampleMeleeWeapon : ModItem
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
        Item.SetWeaponValues(50, 6, 6); // 50 damage, 6 knockback and 6% bonus critical strike chance.

        Item.value = Item.sellPrice(gold: 1);
        Item.rare = 2; // The rarity of the item. 2 is ItemRarityID.Green.
        Item.UseSound = SoundID.Item1;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
