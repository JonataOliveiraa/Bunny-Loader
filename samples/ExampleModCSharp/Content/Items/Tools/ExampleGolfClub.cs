using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent.Golf;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools
{

    public class ExampleGolfClub : ModItem
    {
        public override void SetStaticDefaults()
        {

            ItemID.Sets.CanBePlacedOnWeaponRacks[Type] = true;

            ItemID.Sets.IsAGolfClub[Type] = true;
        }

        public override void SetDefaults()
        {

            Item.DefaultToGolfClub(20, 20);

            Item.SetShopValues(ItemRarityColor.Green2, Item.buyPrice(0, 10));
        }

        public override GolfHelper.ClubProperties? GetGolfClubProperties()
        {

            return GolfHelper.ClubProperties.New(
                minimumStrength: new Vector2(0.25f, 0.25f),
                maximumStrength: new Vector2(1.5f, 0.65f),
                roughLandResistance: 0f
            );
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup)
        {

            itemGroup = ContentSamples.CreativeHelper.ItemGroup.Golf;
        }
    }
}
