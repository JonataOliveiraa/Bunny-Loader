using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

// Touch buttons are on-screen buttons the player can add to the phone's controls, the mobile counterpart of ModKeybind.
// Like keybinds, they are registered in Load and stored in static properties so other classes can read them. See ExampleTouchButtonPlayer for how they are used.
// Their display names come from the Keybinds entries of the localization files, like ModKeybind.
public class TouchButtonSystem : ModSystem
{
    public static ModTouchButton ExampleTouchButton { get; private set; }
    public static ModTouchButton ExampleHoldButton { get; private set; }
    public static ModTouchButton ExampleAnimatedButton { get; private set; }
    public static ModTouchJoystick ExampleAimJoystick { get; private set; }
    public static DrawAnimationVertical LoadingAnimation { get; private set; }

    public override void Load()
    {
        ExampleTouchButton = TouchButtonLoader.RegisterButton(Mod, "ExampleTouchButton");
        ExampleHoldButton = TouchButtonLoader.RegisterButton(Mod, "ExampleHoldButton");
        ExampleAnimatedButton = TouchButtonLoader.RegisterButton(Mod, "ExampleAnimatedButton");
        // A joystick is a touch button that can also be dragged in a direction, like the game's own aim joystick.
        ExampleAimJoystick = TouchButtonLoader.RegisterJoystick(Mod, "ExampleAimJoystick");
    }

    public override void SetStaticDefaults()
    {
        // An animated icon is a vertical sprite sheet, the same format as an animated item (see ExampleSoul).
        // Assets/TouchButtons/ExampleAnimatedButton.png has 16 frames of 48x48 pixels, each followed by 2 empty pixels, and each frame is shown for 3 ticks.
        LoadingAnimation = new DrawAnimationVertical(3, 16);
        ExampleAnimatedButton.Animation = LoadingAnimation;
    }

    // Static fields are not cleared when the mod unloads, so we set them back to null here.
    public override void Unload()
    {
        ExampleTouchButton = null;
        ExampleHoldButton = null;
        ExampleAnimatedButton = null;
        ExampleAimJoystick = null;
        LoadingAnimation = default;
    }
}
