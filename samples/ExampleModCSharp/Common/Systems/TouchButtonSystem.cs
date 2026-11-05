using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

public class TouchButtonSystem : ModSystem
{
    public static ModTouchButton ExampleTouchButton { get; private set; }
    public static ModTouchButton ExampleHoldButton { get; private set; }
    public static ModTouchJoystick ExampleAimJoystick { get; private set; }

    public override void Load()
    {
        ExampleTouchButton = TouchButtonLoader.RegisterButton(Mod, "ExampleTouchButton");
        ExampleHoldButton = TouchButtonLoader.RegisterButton(Mod, "ExampleHoldButton");
        ExampleAimJoystick = TouchButtonLoader.RegisterJoystick(Mod, "ExampleAimJoystick");
    }

    public override void Unload()
    {
        ExampleTouchButton = null;
        ExampleHoldButton = null;
        ExampleAimJoystick = null;
    }
}
