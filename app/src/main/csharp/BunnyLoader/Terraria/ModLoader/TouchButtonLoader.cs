using System;
using System.Collections.Generic;
using System.Threading;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class TouchButtonLoader
{
    internal const int QuickBase = 100;

    static readonly object Gate = new();
    static readonly List<ModTouchButton> buttons = new();
    static readonly Dictionary<string, ModTouchButton> byKey = new();
    static readonly Dictionary<ModKeybind, Texture2D> textures = new();
    static ModTouchButton[] snapshot = Array.Empty<ModTouchButton>();
    static uint tick = uint.MaxValue;

    internal static int Unbound => (int)VirtualControlsHardwareConfigurationMapping_Layout.ContolType.Count;

    internal static int FirstAction => Unbound + 1;

    internal static int QuickUnbound => (int)QuickActionButton_Layout.QuickActionControl.Unbound;

    internal static int FirstControl => (int)GUIVirtualInputController.ControlType.Count;

    public static int Count => Volatile.Read(ref snapshot).Length;

    public static IReadOnlyList<ModTouchButton> Buttons => Volatile.Read(ref snapshot);

    public static ModTouchButton RegisterButton(Mod mod, string name) => Register(mod, name, "");

    public static ModTouchJoystick RegisterJoystick(Mod mod, string name) => (ModTouchJoystick)Register(mod, name, "", true);

    internal static ModTouchButton Register(Mod mod, string name, string defaultBinding, bool joystick = false)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("the button needs a name", nameof(name));
        name = name.Trim();
        string key = $"{mod.Name}/{name}";
        ModTouchButton button;
        lock (Gate)
        {
            if (byKey.TryGetValue(key, out ModTouchButton? known)) return known;
            button = joystick ? new ModTouchJoystick(mod, name) : new ModTouchButton(mod, name, defaultBinding);
            button.Index = buttons.Count;
            buttons.Add(button);
            byKey[key] = button;
            Volatile.Write(ref snapshot, buttons.ToArray());
        }
        TouchButtonHooks.Install();
        return button;
    }

    public static int ActionOf(ModKeybind button) => FirstAction + button.Index;

    internal static int QuickOf(ModKeybind button) => QuickBase + button.Index;

    internal static int ControlOf(ModKeybind button) => FirstControl + button.Index;

    internal static int LastAction => FirstAction + Count - 1;

    internal static ModTouchButton? ByKey(string? key)
    {
        if (key == null) return null;
        lock (Gate) return byKey.GetValueOrDefault(key);
    }

    internal static ModTouchButton? ByAction(int action)
    {
        ModTouchButton[] all = Volatile.Read(ref snapshot);
        int i = action - FirstAction;
        return i >= 0 && i < all.Length ? all[i] : null;
    }

    internal static ModTouchButton? ByQuick(int quick)
    {
        ModTouchButton[] all = Volatile.Read(ref snapshot);
        int i = quick - QuickBase;
        return i >= 0 && i < all.Length ? all[i] : null;
    }

    internal static bool IsModded(int action) => action >= FirstAction;

    internal static string Label(ModKeybind keybind)
    {
        if (keybind is ModTouchButton { Label: not null } button)
        {
            try
            {
                string own = button.Label();
                if (!string.IsNullOrEmpty(own)) return own;
            }
            catch (Exception e) { ItemHooks.Report($"{keybind.FullName}.Label", e); }
        }
        foreach (string category in new[] { "TouchButtons", "Keybinds" })
        {
            string textKey = $"Mods.{keybind.Mod.Name}.{category}.{keybind.Name}.DisplayName";
            if (Language.Exists(textKey)) return Language.GetTextValue(textKey);
        }
        return keybind.PrettyName();
    }

    internal static bool Visible(ModTouchButton button) => Ask(button, button.Visible, "Visible");

    internal static bool Enabled(ModTouchButton button) => Ask(button, button.Enabled, "Enabled");

    static bool Ask(ModTouchButton button, Func<bool>? question, string what)
    {
        if (question == null) return true;
        try { return question(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{button.FullName}.{what}", e);
            return true;
        }
    }

    internal static Texture2D Texture(ModTouchButton button)
    {
        lock (Gate)
            if (textures.TryGetValue(button, out Texture2D known)) return known;
        Texture2D texture = default;
        try { texture = ResolveTexture(button); }
        catch (Exception e) { ItemHooks.Report($"{button.FullName}.Icon", e); }
        lock (Gate) textures[button] = texture;
        return texture;
    }

    static Texture2D ResolveTexture(ModTouchButton button)
    {
        string mod = button.Mod.Name;
        string[] candidates = button.Icon != null
            ? new[] { button.Icon }
            : new[] { $"Assets/TouchButtons/{button.Name}", "icon_small", "icon" };
        foreach (string path in candidates)
        {
            string name = path.StartsWith(mod + "/") ? path : $"{mod}/{path}";
            if (ModContent.RequestIfExists(name, out Asset<Texture2D> asset, AssetRequestMode.ImmediateLoad)) return asset.Value;
        }
        if (button.Icon != null) Log.Warn($"{button.FullName}: icon '{button.Icon}' is not in the mod");
        return default;
    }

    internal static void Tick()
    {
        uint now = Main.GameUpdateCount;
        if (now == tick) return;
        tick = now;
        ModTouchButton[] all = Volatile.Read(ref snapshot);
        if (all.Length == 0) return;
        GameArray<bool> pressed = default;
        GUIVirtualInputController controller = Controller();
        if (!controller.IsNull) pressed = controller.Pressed;
        bool active = !Main.gameMenu;
        foreach (ModTouchButton button in all)
        {
            int control = ControlOf(button);
            bool held = !pressed.IsNull && control < pressed.Length && pressed[control];
            bool current = active && (button.latch || held) && Enabled(button);
            button.latch = false;
            button.old = button.current;
            button.current = current;
            button.justPressed = current && !button.old;
            button.justReleased = !current && button.old;
        }
    }

    internal static void Press(ModTouchButton button)
    {
        if (Main.gameMenu || !Enabled(button)) return;
        button.latch = true;
        if (button.OnPress == null) return;
        try { button.OnPress(Main.player[Main.myPlayer]); }
        catch (Exception e) { ItemHooks.Report($"{button.FullName}.OnPress", e); }
    }

    internal static GUIVirtualInputController Controller()
    {
        GUIInstance gui = GUIInstance.Active;
        return gui.IsNull ? default : gui.GUIVirtualInputController;
    }

    internal static VirtualControlsStyles_Layout Styles() => VirtualControlsStyles_Layout.Instance;

    internal static bool IsBound(ModKeybind keybind)
    {
        VirtualControlsStyles_Layout styles = Styles();
        if (styles.IsNull) return false;
        VirtualControlsProfile_Layout profile = styles.ActiveProfile;
        if (profile.IsNull) return false;
        int action = ActionOf(keybind);
        GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
        for (int i = 0; !controls.IsNull && i < controls.Length; i++)
        {
            VirtualControlsBinding_Layout binding = controls[i];
            if (binding.IsNull) continue;
            if ((int)binding.Action == action) return true;
            GameArray<VirtualControlsHardwareConfigurationMapping_Layout.ContolType> groups = binding.GroupActions;
            for (int j = 0; !groups.IsNull && j < groups.Length; j++)
                if ((int)groups[j] == action) return true;
        }
        return false;
    }

    internal static void ContentReady()
    {
        if (Count == 0) return;
        TouchButtonHooks.ApplySaved();
    }
}
