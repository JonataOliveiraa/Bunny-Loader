using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories
{

    public class ExamplePotionDelayAccessory : ModItem
    {

        public static readonly int FlatDelayDecrease = 10;
        public static readonly int MultiplicativeDelayDecrease = 15;

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(FlatDelayDecrease, MultiplicativeDelayDecrease);

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {

            player.PotionDelayModifier.Flat -= FlatDelayDecrease * 60;

            player.PotionDelayModifier *= 1 - MultiplicativeDelayDecrease / 100f;
        }
    }
}
