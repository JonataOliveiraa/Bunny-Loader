using ExampleMod.Content.DamageClasses;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalItems
{

    public class ExampleResearchSorting : GlobalItem
    {

        public override void ModifyResearchSorting(Item item, ref ContentSamples.CreativeHelper.ItemGroup itemGroup) {
            if (item.DamageType.CountsAsClass<ExampleDamageClass>()) {
                itemGroup = (ContentSamples.CreativeHelper.ItemGroup)531; 
            }

            if (item.type == ItemID.CopperShortsword) {
                itemGroup = ContentSamples.CreativeHelper.ItemGroup.EventItem; 
                
            }

        }
    }
}
