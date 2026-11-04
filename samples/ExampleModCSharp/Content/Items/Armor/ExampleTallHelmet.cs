using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor
{

    [AutoloadEquip(EquipType.Head)]
    public class ExampleTallHelmet : ModItem
    {
        public override void SetStaticDefaults() {
            ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

        }

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 24;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
            Item.defense = 5;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
