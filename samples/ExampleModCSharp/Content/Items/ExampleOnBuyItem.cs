using ItemCreationContext = Terraria.ModLoader.ItemCreationContext;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ExampleOnBuyItem : ModItem
    {
        public static LocalizedText DeathMessage { get; private set; }

        public override void SetStaticDefaults() {
            DeathMessage = this.GetLocalization(nameof(DeathMessage));
        }

        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 1, copper: 50);
            Item.maxStack = 9999;
        }

        public override void OnCreated(ItemCreationContext context) {
            if (context is not BuyItemCreationContext buyContext) {
                return;
            }

            if (!Main.rand.NextBool()) {
                return;
            }

            Player player = Main.LocalPlayer;
            player.KillMe(PlayerDeathReason.ByCustomReason(DeathMessage.Format(player.name)), 9999, 0);
        }
    }
}
