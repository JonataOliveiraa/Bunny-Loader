using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

public class ExampleTooltipsItem : ModItem
{
    public static LocalizedText RemoveMeText { get; private set; }
    public static LocalizedText FaceText { get; private set; }

    public override void SetStaticDefaults()
    {
        Main.RegisterItemAnimation(Type, new DrawAnimationVertical(30, 4));
        ItemID.Sets.AnimatesAsSoul[Type] = true;
        ItemID.Sets.ItemNoGravity[Type] = true;

        RemoveMeText = this.GetLocalization("RemoveMe");
        FaceText = this.GetLocalization("Face");
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.value = Item.sellPrice(silver: 1);
        Item.rare = ItemRarityID.Blue;
    }

    public override Color? GetAlpha(Color lightColor) => Color.White;

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var line = new TooltipLine(Mod, "Verbose:RemoveMe", RemoveMeText.Value);
        tooltips.Add(line);

        line = new TooltipLine(Mod, "Face", FaceText.Value)
        {
            Color = new Color(100, 100, 255)
        };
        tooltips.Add(line);

        foreach (TooltipLine line2 in tooltips)
        {
            if (line2.Mod == "Terraria" && line2.Name == "ItemName")
            {
                line2.Color = Main.DiscoColor;
            }
        }

        foreach (var l in tooltips)
        {
            if (l.Name.EndsWith(":RemoveMe"))
            {
                l.Hide();
            }
        }
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
