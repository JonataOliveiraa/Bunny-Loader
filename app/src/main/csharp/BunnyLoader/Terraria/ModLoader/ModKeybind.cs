using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Terraria.GameInput;
using Terraria.Localization;

namespace Terraria.ModLoader;

public class ModKeybind
{
    internal bool current, old, justPressed, justReleased, latch;

    internal ModKeybind(Mod mod, string name, string defaultBinding)
    {
        Mod = mod;
        Name = name;
        DefaultBinding = defaultBinding;
    }

    internal int Index { get; set; }

    internal Mod Mod { get; }

    internal string Name { get; }

    internal string FullName => $"{Mod.Name}/{Name}";

    internal string DefaultBinding { get; }

    public LocalizedText DisplayName => Language.GetOrRegister($"Mods.{Mod.Name}.Keybinds.{Name}.DisplayName", PrettyName);

    internal string PrettyName() => Regex.Replace(Name, "([A-Z])", " $1").Trim();

    public List<string> GetAssignedKeys(InputMode mode = InputMode.Keyboard) =>
        TouchButtonLoader.IsBound(this) ? new List<string> { TouchButtonLoader.Label(this) } : new List<string>();

    public bool RetroCurrent
    {
        get
        {
            Player player = Main.player[Main.myPlayer];
            if (Main.drawingPlayerChat || player.talkNPC != -1 || player.sign != -1) return false;
            return Current;
        }
    }

    public bool Current
    {
        get
        {
            TouchButtonLoader.Tick();
            return current;
        }
    }

    public bool JustPressed
    {
        get
        {
            TouchButtonLoader.Tick();
            return justPressed;
        }
    }

    public bool JustReleased
    {
        get
        {
            TouchButtonLoader.Tick();
            return justReleased;
        }
    }

    public bool Old
    {
        get
        {
            TouchButtonLoader.Tick();
            return old;
        }
    }

    public override string ToString() => FullName;
}

public sealed class ModTouchButton : ModKeybind
{
    internal ModTouchButton(Mod mod, string name, string defaultBinding) : base(mod, name, defaultBinding) { }

    public new Mod Mod => base.Mod;

    public new string Name => base.Name;

    public new string FullName => base.FullName;

    public string? Icon { get; set; }

    public Func<string>? Label { get; set; }

    public Func<bool>? Visible { get; set; }

    public Func<bool>? Enabled { get; set; }

    public Action<Player>? OnPress { get; set; }

    public bool IsBound => TouchButtonLoader.IsBound(this);
}
