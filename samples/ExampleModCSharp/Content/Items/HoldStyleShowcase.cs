using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class HoldStyleShowcase : ModItem
    {
        public static LocalizedText SwitchingText { get; private set; }
        public static LocalizedText ThisIsText { get; private set; }

        public override string Texture => "ExampleMod/Content/Items/Weapons/ExampleSword";

        public override void SetStaticDefaults() {
            SwitchingText = this.GetLocalization("Switching");
            ThisIsText = this.GetLocalization("ThisIs");

            ItemID.Sets.HasRightFire[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;

            Item.holdStyle = ItemHoldStyleID.None;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write((byte)Item.holdStyle);
        }

        public override void NetReceive(BinaryReader reader) {
            Item.holdStyle = reader.ReadByte();
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }

            if (player.altFunctionUse == 2) {
                Item.holdStyle++;
                if (Item.holdStyle > ItemHoldStyleID.HoldOrb) {
                    Item.holdStyle = ItemHoldStyleID.None;
                }
                Main.NewText(SwitchingText.Format(Item.holdStyle));

                Item.NetStateChanged();
            }
            else {
                Main.NewText(ThisIsText.Format(Item.holdStyle));
            }
            return true;
        }
    }
}
