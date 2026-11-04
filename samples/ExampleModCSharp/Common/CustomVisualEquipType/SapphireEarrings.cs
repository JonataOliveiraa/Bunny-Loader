using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;

namespace ExampleMod.Common.CustomVisualEquipType
{

    public class SapphireEarrings : ModItem
    {
        public override void SetStaticDefaults() {

            EarringsLoader.AddEarringTexture(Type, Texture + "_Earrings");
        }

        public override void SetDefaults() {
            Item.DefaultToAccessory(16, 16);
            Item.SetShopValues(ItemRarityColor.Orange3, Item.buyPrice(gold: 6));
            Item.vanity = true;
        }
    }
}
