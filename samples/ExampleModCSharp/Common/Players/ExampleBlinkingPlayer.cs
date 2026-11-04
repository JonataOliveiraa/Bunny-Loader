using Terraria.GameContent;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class ExampleBlinkingPlayer : ModPlayer
    {
        public override void PostUpdate() {

            if (Player.eyeHelper.CurrentEyeState == PlayerEyeHelper.EyeState.NormalBlinking && Player.wet) {
                Player.eyeHelper.CurrentEyeFrame = PlayerEyeHelper.EyeFrame.EyeClosed;
                Player.eyeHelper.TimeInState = 0;
            }

            if (Player.eyeHelper.CurrentEyeState == PlayerEyeHelper.EyeState.IsBlind) {

                if ((Player.eyeHelper.TimeInState % 120 - 115) < 0) {
                    Player.eyeHelper.CurrentEyeFrame = PlayerEyeHelper.EyeFrame.EyeClosed;
                }
                else {
                    Player.eyeHelper.CurrentEyeFrame = PlayerEyeHelper.EyeFrame.EyeHalfClosed;
                }
            }
        }
    }
}
