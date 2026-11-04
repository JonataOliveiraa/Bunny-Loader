using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.BuilderToggles
{

    public class ExampleBuilderToggle : BuilderToggle
    {
        public static LocalizedText CurrentColorText { get; private set; }
        public static LocalizedText[] ColorText { get; private set; }

        public override void SetStaticDefaults() {
            CurrentColorText = this.GetLocalization("CurrentColor");
            ColorText = Enumerable.Range(0, 4).Select(i => this.GetLocalization($"Color_{i}")).ToArray();
        }

        public override bool Active() => Main.LocalPlayer.HeldItem.IsAir;

        public override int NumberOfStates => 4;

        public override string DisplayValue() {
            return CurrentColorText.Format(ColorText[CurrentState].Value);
        }

        public override bool Draw(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
            Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];
            drawParams.Color = colors[CurrentState];
            return true;
        }

        public override void OnRightClick() {
            CurrentState -= 1;
            if (CurrentState < 0) {
                CurrentState = NumberOfStates - 1;
            }

            SoundEngine.PlaySound(SoundID.Coins);
        }
    }

    public class ExampleBuilderToggleDimmedLight : BuilderToggle
    {
        public static LocalizedText OnText { get; private set; }
        public static LocalizedText OffText { get; private set; }

        public override string Texture => "ExampleMod/Content/BuilderToggles/ExampleBuilderToggle";
        public override bool Active() => true;
        public override int NumberOfStates => 2;

        public override void SetStaticDefaults() {
            OnText = this.GetLocalization(nameof(OnText));
            OffText = this.GetLocalization(nameof(OffText));
        }

        public override string DisplayValue() {
            return CurrentState == 0 ? OnText.Value : OffText.Value;
        }

        public override bool Draw(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
            drawParams.Color = CurrentState == 0 ? Color.White : new Color(127, 127, 127);
            return true;
        }
    }
}
