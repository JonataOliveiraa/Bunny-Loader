using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BunnyLoader;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class MenuLoader
{
    const string SaveFile = "BunnyLoader.menu.json";

    static readonly List<ModMenu> Menus = new();
    static readonly ModMenu Vanilla = new VanillaMenu();
    static ModMenu? current;
    static string? chosen;
    static bool restored;

    internal static bool Ready;

    public static ModMenu CurrentMenu => current ?? Vanilla;

    internal static IReadOnlyList<ModMenu> All => Menus;

    internal static void Add(ModMenu menu)
    {
        Menus.Add(menu);
        MenuHooks.Install(menu.GetType());
    }

    internal static string KeyOf(ModMenu menu) => menu == Vanilla ? "Terraria" : menu.FullName;

    internal static string NameOf(ModMenu menu)
    {
        try { return menu.DisplayName; }
        catch (Exception e)
        {
            ItemHooks.Report($"{menu.FullName}.DisplayName", e);
            return menu.Name;
        }
    }

    static bool Available(ModMenu menu)
    {
        if (menu == Vanilla) return true;
        try { return menu.IsAvailable; }
        catch (Exception e)
        {
            ItemHooks.Report($"{menu.FullName}.IsAvailable", e);
            return false;
        }
    }

    internal static ModMenu? Active() => current != null && current != Vanilla && Main.gameMenu ? current : null;

    internal static ModMenu Next()
    {
        var all = new List<ModMenu>(Menus.Count + 1) { Vanilla };
        all.AddRange(Menus);
        int index = all.IndexOf(CurrentMenu);
        for (int n = 0; n < all.Count; n++)
        {
            index = (index + 1) % all.Count;
            if (!Available(all[index])) continue;
            chosen = KeyOf(all[index]);
            Save();
            Switch(all[index]);
            return all[index];
        }
        return CurrentMenu;
    }

    internal static void Tick()
    {
        Switch(Pick());
        ModMenu menu = CurrentMenu;
        try { menu.Update(Main.menuMode == 0); }
        catch (Exception e) { ItemHooks.Report($"{menu.FullName}.Update", e); }
    }

    internal static int BackgroundSlot()
    {
        if (Active() is not ModMenu menu || WorldGen.drunkWorldGen) return -1;
        try
        {
            ModSurfaceBackgroundStyle? style = menu.MenuBackgroundStyle;
            return style != null && SurfaceBackgroundStylesLoader.Get(style.Slot) == style ? style.Slot : -1;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{menu.FullName}.MenuBackgroundStyle", e);
            return -1;
        }
    }

    internal static int MenuMusic()
    {
        if (Active() is not ModMenu menu) return -1;
        if (WorldGen.drunkWorldGen || WorldGen.remixWorldGen || WorldGen.tenthAnniversaryWorldGen || Main.menuMode == 3000) return -1;
        int music;
        try { music = menu.Music; }
        catch (Exception e)
        {
            ItemHooks.Report($"{menu.FullName}.Music", e);
            return -1;
        }
        return music is MusicID.TitleJourneysBeginningWithIntro or MusicID.TitleJourneysBeginning || music < 0 ? -1 : music;
    }

    static ModMenu Pick()
    {
        if (!restored) Restore();
        if (chosen != null)
        {
            if (chosen == "Terraria") return Vanilla;
            foreach (ModMenu menu in Menus)
                if (KeyOf(menu) == chosen && Available(menu)) return menu;
        }
        for (int i = Menus.Count - 1; i >= 0; i--)
            if (Available(Menus[i])) return Menus[i];
        return Vanilla;
    }

    static void Switch(ModMenu next)
    {
        ModMenu previous = CurrentMenu;
        current = next;
        if (next == previous) return;
        try { previous.OnDeselected(); }
        catch (Exception e) { ItemHooks.Report($"{previous.FullName}.OnDeselected", e); }
        try { next.OnSelected(); }
        catch (Exception e) { ItemHooks.Report($"{next.FullName}.OnSelected", e); }
    }

    static string? FilePath() => Main.SavePath is { Length: > 0 } folder ? Path.Combine(folder, SaveFile) : null;

    static void Restore()
    {
        restored = true;
        chosen = null;
        try
        {
            if (FilePath() is not string file || !File.Exists(file)) return;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.TryGetProperty("Theme", out JsonElement theme) && theme.ValueKind == JsonValueKind.String) chosen = theme.GetString();
        }
        catch (Exception e) { Log.Warn($"ModMenu: {SaveFile} could not be read ({e.Message}); using the last mod theme"); }
    }

    static void Save()
    {
        try
        {
            if (FilePath() is string file) File.WriteAllText(file, JsonSerializer.Serialize(new Dictionary<string, string?> { ["Theme"] = chosen }));
        }
        catch (Exception e) { Log.Warn($"ModMenu: {SaveFile} could not be written ({e.Message})"); }
    }
}
