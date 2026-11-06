using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class UseStyleShowcase : ModItem
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
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write((byte)Item.useStyle);
        }

        public override void NetReceive(BinaryReader reader) {
            Item.useStyle = reader.ReadByte();
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }

            if (player.altFunctionUse == 2) {
                Item.useStyle++;
                if (Item.useStyle > ItemUseStyleID.PlaySound) {
                    Item.useStyle = ItemUseStyleID.Swing;
                }
                string useStyleName = ItemUseStyleID.Search.GetName(Item.useStyle);
                Main.NewText(SwitchingText.Format(Item.useStyle) + $" ({useStyleName})");

                Item.NetStateChanged();
            }
            else {
                Main.NewText(ThisIsText.Format(Item.useStyle) + $" ({ItemUseStyleID.Search.GetName(Item.useStyle)})");
            }
            return true;
        }
    }
}
