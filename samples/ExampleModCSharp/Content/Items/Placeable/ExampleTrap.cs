using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleTrap : ModItem, ICustomAutoload
{
    public static void Autoload(Mod mod)
    {
        mod.AddContent(new ExampleTrap(0));
        mod.AddContent(new ExampleTrap(1));
    }

    protected override bool CloneNewInstances => true;
    private readonly int placeStyle;

    public override string Name => GetInternalNameFromStyle(placeStyle);

    public static string GetInternalNameFromStyle(int style)
    {
        if (style == 0)
        {
            return "ExampleTrapIchorBullet";
        }
        if (style == 1)
        {
            return "ExampleTrapChlorophyteBullet";
        }
        throw new Exception("Invalid style");
    }

    public ExampleTrap(int placeStyle)
    {
        this.placeStyle = placeStyle;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleTrap>(), placeStyle);

        Item.width = 12;
        Item.height = 12;
        Item.value = 10000;
        Item.mech = true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.DartTrap)
            .Register();
    }
}
