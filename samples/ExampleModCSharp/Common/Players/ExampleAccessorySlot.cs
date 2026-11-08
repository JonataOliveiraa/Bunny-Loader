using ExampleMod.Common.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{
    public class ExampleModAccessorySlot1 : ModAccessorySlot
    {
    }

    public class ExampleCustomLocationAndTextureSlot : ModAccessorySlot
    {
        public override Vector2? CustomLocation => new Vector2(Main.screenWidth / 2, 3 * Main.screenHeight / 4);

        public override bool DrawVanitySlot => !DyeItem.IsAir;

        public override string VanityBackgroundTexture => "Terraria/Images/Inventory_Back14";
        public override string FunctionalBackgroundTexture => "Terraria/Images/Inventory_Back7";
        public override string DyeBackgroundTexture => "Terraria/Images/Inventory_Back13";

        public override string VanityTexture => "Terraria/Images/Item_" + ItemID.PiggyBank;

        public override bool IsHidden() {
            return IsEmpty;
        }

        public override void BackgroundDrawColor(AccessorySlotType context, ref Color color) {
            if (context == AccessorySlotType.DyeSlot) {
                color = Main.DiscoColor * (Main.invAlpha / 255);
            }
        }
    }

    public class ExampleModWingSlot : ModAccessorySlot
    {
        public static LocalizedText WingsText { get; private set; }
        public static LocalizedText WingsDyeText { get; private set; }

        public override bool HasEquipmentLoadoutSupport => ModContent.GetInstance<ExampleModConfig>().WingSlotLoadoutSupportToggle;

        public override void SetupContent() {
            WingsText = Mod.GetLocalization($"{nameof(ExampleModWingSlot)}.Wings");
            WingsDyeText = Mod.GetLocalization($"{nameof(ExampleModWingSlot)}.WingsDye");
        }

        public override bool CanAcceptItem(Item checkItem, AccessorySlotType context) {
            if (checkItem.wingSlot > 0)
                return true;

            return false;
        }

        public override bool ModifyDefaultSwapSlot(Item item, int accSlotToSwapTo) {
            if (item.wingSlot > 0)
                return true;

            return false;
        }

        public override bool IsEnabled() {
            if (Player.GetEffectiveArmor(0).headSlot >= 0)
                return true;

            return false;
        }

        public override bool IsVisibleWhenNotEnabled() {
            return false;
        }

        public override string FunctionalTexture => "Terraria/Images/Item_" + ItemID.CreativeWings;

        public override void OnMouseHover(AccessorySlotType context) {
            switch (context) {
                case AccessorySlotType.FunctionalSlot:
                case AccessorySlotType.VanitySlot:
                    Main.hoverItemName = WingsText.Value;
                    break;
                case AccessorySlotType.DyeSlot:
                    Main.hoverItemName = WingsDyeText.Value;
                    break;
            }
        }
    }
}
