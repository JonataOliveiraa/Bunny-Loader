using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.CustomVisualEquipType
{

    public class EarringsGlobalItem : GlobalItem
    {
        public override void UpdateVisibleAccessory(Item item, Player player, bool hideVisual) {

            if (hideVisual) {
                return;
            }

            if (EarringsLoader.earringItemToTexture.ContainsKey(item.type)) {
                player.GetModPlayer<EarringsPlayer>().earring = item.type;
            }
        }

        public override void UpdateItemDye(Item item, Player player, int dye, bool hideVisual) {

            if (hideVisual) {
                return;
            }

            if (EarringsLoader.earringItemToTexture.ContainsKey(item.type)) {
                player.GetModPlayer<EarringsPlayer>().earringShader = dye;
            }
        }
    }
}
