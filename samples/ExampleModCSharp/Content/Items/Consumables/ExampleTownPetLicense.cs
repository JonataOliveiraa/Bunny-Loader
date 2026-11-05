using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Common.Systems;
using ExampleMod.Content.NPCs.TownPets;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables
{
    public class ExampleTownPetLicense : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 5;
        }

        public override void SetDefaults() {
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.UseSound = SoundID.Item92;
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = Item.CommonMaxStack;
            Item.SetShopValues(ItemRarityColor.Green2, Item.buyPrice(0, 5));
        }

        public override bool? UseItem(Player player) {
            int npcType = ModContent.NPCType<ExampleTownPet>();
            if (player.ItemAnimationJustStarted && (!ExampleTownPetSystem.boughtExampleTownPet || NPC.AnyNPCs(npcType))) {
                if (player.whoAmI == Main.myPlayer) {
                    ExampleTownPetUnlockOrExchangePet(ref ExampleTownPetSystem.boughtExampleTownPet, npcType, this.GetLocalizationKey("LicenseExampleTownPetUse"));
                }
                return true;
            }
            return false;
        }

        public static void ExampleTownPetUnlockOrExchangePet(ref bool petBoughtFlag, int npcType, string textKeyForLicense) {
            Color color = new(50, 255, 130);
            if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {
                if (!petBoughtFlag || NPC.AnyNPCs(npcType)) {
                    ModPacket packet = ModContent.GetInstance<ExampleMod>().GetPacket();
                    packet.Write((byte)ExampleMod.MessageType.ExampleTownPetUnlockOrExchange);
                    packet.Send();
                }
            }
            else if (!petBoughtFlag) {
                petBoughtFlag = true;
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey(textKeyForLicense), color);
                NetMessage.TrySendData(MessageID.WorldData);
            }
            else if (NPC.RerollVariationForNPCType(npcType)) {
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Misc.PetExchangeSuccess"), color);
            }
            else {
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Misc.PetExchangeFail"), color);
            }
        }
    }
}
