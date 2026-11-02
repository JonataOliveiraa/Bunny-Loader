using Microsoft.Xna.Framework.Input;

namespace Terraria.ModLoader;

public static class KeybindLoader
{
    public static ModKeybind RegisterKeybind(Mod mod, string name, Keys defaultBinding) =>
        RegisterKeybind(mod, name, defaultBinding.ToString());

    public static ModKeybind RegisterKeybind(Mod mod, string name, string defaultBinding) =>
        TouchButtonLoader.Register(mod, name, defaultBinding);
}
