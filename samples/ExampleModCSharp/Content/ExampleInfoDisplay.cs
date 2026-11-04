using ExampleMod.Common.Players;
using ExampleMod.Content.Items.Accessories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content
{

    public class ExampleInfoDisplay : InfoDisplay
    {
        public static LocalizedText CurrentMinionsText { get; private set; }
        public static LocalizedText NoMinionsText { get; private set; }

        public override void SetStaticDefaults() {
            CurrentMinionsText = this.GetLocalization("CurrentMinions");
            NoMinionsText = this.GetLocalization("NoMinions");
        }

        public static Color RedInfoTextColor => new(255, 19, 19, Main.mouseTextColor);

        public override string HoverTexture => Texture + "_Hover";

        public override bool Active() {
            return Main.LocalPlayer.GetModPlayer<ExampleInfoDisplayPlayer>().showMinionCount;
        }

        public override string DisplayValue(ref Color displayColor, ref Color displayShadowColor) {

            int minionCount = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.minion && proj.owner == Main.myPlayer) {
                    minionCount++;
                }
            }

            bool noInfo = minionCount == 0;
            if (noInfo) {

                displayColor = InactiveInfoTextColor;
            }
            else if (minionCount < Main.LocalPlayer.maxMinions) {

                displayColor = RedInfoTextColor;
            }

            return !noInfo ? CurrentMinionsText.Format(minionCount) : NoMinionsText.Value;
        }
    }

}
