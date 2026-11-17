using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

// Touch buttons are on-screen buttons the player can add to the phone's controls, the mobile counterpart of ModKeybind.
// Like keybinds, they are registered in Load and stored in static properties so other classes can read them. See ExampleTouchButtonPlayer for how they are used.
// Their display names come from the Keybinds entries of the localization files, like ModKeybind.
public class TouchButtonSystem : ModSystem
{
    public static ModTouchButton ExampleTouchButton { get; private set; }
    public static ModTouchButton ExampleHoldButton { get; private set; }
    public static ModTouchJoystick ExampleAimJoystick { get; private set; }

    public override void Load()
    {
        ExampleTouchButton = TouchButtonLoader.RegisterButton(Mod, "ExampleTouchButton");
        ExampleHoldButton = TouchButtonLoader.RegisterButton(Mod, "ExampleHoldButton");
        // A joystick is a touch button that can also be dragged in a direction, like the game's own aim joystick.
        ExampleAimJoystick = TouchButtonLoader.RegisterJoystick(Mod, "ExampleAimJoystick");
    }

    // Static fields are not cleared when the mod unloads, so we set them back to null here.
    public override void Unload()
    {
        ExampleTouchButton = null;
        ExampleHoldButton = null;
        ExampleAimJoystick = null;
    }
}
