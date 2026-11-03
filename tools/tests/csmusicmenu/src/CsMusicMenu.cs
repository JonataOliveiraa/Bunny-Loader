using System;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsMusicMenu;

public sealed class MenuSurface : ModSurfaceBackgroundStyle
{
    public override void ModifyFarFades(float[] fades, float transitionSpeed)
    {
        for (int i = 0; i < fades.Length; i++)
            fades[i] = i == Slot ? Math.Min(1f, fades[i] + transitionSpeed) : Math.Max(0f, fades[i] - transitionSpeed);
    }

    public override int ChooseFarTexture() => BackgroundTextureLoader.GetBackgroundSlot(Mod, "Backgrounds/ExampleBiomeSurfaceFar");
}

public sealed class TestMenu : ModMenu
{
    public static int Selected, Deselected, Updates, TitleUpdates, PreLogo, PostLogo;
    public static Color PostColor;

    Asset<Texture2D> sun = null!;

    public override void Load() => sun = ModContent.Request<Texture2D>("CsMusicMenu/Sun");

    public override Asset<Texture2D> SunTexture => sun;

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/Tone");

    public override ModSurfaceBackgroundStyle MenuBackgroundStyle => ModContent.GetInstance<MenuSurface>();

    public override string DisplayName => "Test Menu";

    public override void OnSelected() => Selected++;

    public override void OnDeselected() => Deselected++;

    public override void Update(bool isOnTitleScreen)
    {
        Updates++;
        if (isOnTitleScreen) TitleUpdates++;
    }

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        PreLogo++;
        drawColor = new Color(255, 0, 0, 255);
        return true;
    }

    public override void PostDrawLogo(SpriteBatch spriteBatch, Vector2 logoDrawCenter, float logoRotation, float logoScale, Color drawColor)
    {
        PostLogo++;
        PostColor = drawColor;
    }
}

public sealed class BoxTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
    }
}

public sealed class BoxItem : ModItem
{
    public static string Checks = "";

    public override void SetStaticDefaults()
    {
        int slot = MusicLoader.GetMusicSlot(Mod, "Music/Tone");
        MusicLoader.AddMusicBox(Mod, slot, Type, ModContent.TileType<BoxTile>());
        Checks += Throws<ArgumentOutOfRangeException>(() => MusicLoader.AddMusicBox(Mod, MusicID.OverworldDay, Type, ModContent.TileType<BoxTile>())) ? "vanilla-music " : "";
        Checks += Throws<ArgumentException>(() => MusicLoader.AddMusicBox(Mod, slot, Type, ModContent.TileType<BoxTile>(), 36)) ? "twice " : "";
        Checks += Throws<ArgumentOutOfRangeException>(() => MusicLoader.AddMusicBox(Mod, slot, ItemID.MusicBox, ModContent.TileType<BoxTile>(), 36)) ? "vanilla-item " : "";
    }

    static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return true; }
        catch (Exception) { return false; }
        return false;
    }

    public override void SetDefaults() => Item.DefaultToMusicBox(ModContent.TileType<BoxTile>(), 0);
}

public sealed class CsMusicMenuMod : Mod
{
    static int frames, logos, pass, fail;
    static bool done, menuChecked;
    static int boxX, boxY, selectedBase, deselectedBase;

    static ILog Log => ModContent.GetInstance<CsMusicMenuMod>().Logger;

    static readonly Assembly Api = typeof(ModMenu).Assembly;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUILogo.Draw += (orig, self) =>
        {
            orig(self);
            if (!Main.gameMenu || menuChecked) return;
            try { Menu(++logos); }
            catch (Exception e)
            {
                Report("menu run", false, e.ToString());
                menuChecked = true;
            }
        };
    }

    static object? Call(string type, string member, params object[] args)
    {
        Type t = Api.GetType(type, true)!;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
        if (t.GetProperty(member, flags) is PropertyInfo property) return property.GetValue(null);
        return t.GetMethod(member, flags)!.Invoke(null, args);
    }

    static int Wanted => (int)Call("Terraria.ModLoader.Core.MusicHooks", "Wanted")!;

    static int Tone => MusicLoader.GetMusicSlot(ModContent.GetInstance<CsMusicMenuMod>(), "Music/Tone");

    static void Menu(int n)
    {
        var menu = ModContent.GetInstance<TestMenu>();
        if (n == 1)
        {
            Type loader = Api.GetType("Terraria.ModLoader.MenuLoader", true)!;
            loader.GetField("chosen", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
            loader.GetField("restored", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
            selectedBase = TestMenu.Selected - (MenuLoader.CurrentMenu == menu && TestMenu.Selected > 0 ? 1 : 0);
            deselectedBase = TestMenu.Deselected;
            return;
        }
        int selected = TestMenu.Selected - selectedBase, deselected = TestMenu.Deselected - deselectedBase;
        switch (n)
        {
            case 40:
                Report("the last mod theme is chosen and selected once", MenuLoader.CurrentMenu == menu && menu.IsSelected && selected == 1,
                    $"current={MenuLoader.CurrentMenu.FullName} selected={selected}");
                Report("Update runs every menu frame, on the title screen too", TestMenu.Updates >= 30 && TestMenu.TitleUpdates > 0, $"updates={TestMenu.Updates} title={TestMenu.TitleUpdates}");
                Report("PreDrawLogo and PostDrawLogo run with the changed color", TestMenu.PreLogo >= 30 && TestMenu.PostLogo >= 30 && TestMenu.PostColor.R == 255 && TestMenu.PostColor.G == 0,
                    $"pre={TestMenu.PreLogo} post={TestMenu.PostLogo} color={TestMenu.PostColor}");
                Report("the theme's background style is the menu's bgStyle", Main.bgStyle == ModContent.GetInstance<MenuSurface>().Slot, $"bgStyle={Main.bgStyle}");
                Report("the theme's music is the menu's", Wanted == Tone, $"wanted={Wanted} tone={Tone}");
                Call("Terraria.ModLoader.MenuLoader", "Next");
                break;
            case 45:
                Report("Next goes to the game's title and deselects the theme", MenuLoader.CurrentMenu != menu && MenuLoader.CurrentMenu.DisplayName == "Terraria" && deselected == 1,
                    $"current={MenuLoader.CurrentMenu.DisplayName} deselected={deselected}");
                break;
            case 55:
                Report("with the game's title, the game's music", Wanted != Tone, $"wanted={Wanted}");
                Call("Terraria.ModLoader.MenuLoader", "Next");
                break;
            case 60:
                Report("Next comes back to the theme", MenuLoader.CurrentMenu == menu && selected == 2, $"current={MenuLoader.CurrentMenu.FullName} selected={selected}");
                menuChecked = true;
                break;
        }
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { World(self, ++frames); }
        catch (Exception e)
        {
            Report("world run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csmusicmenu END pass={pass} fail={fail}");
    }

    static bool BoxOn()
    {
        for (int x = boxX; x < boxX + 2; x++)
            for (int y = boxY; y < boxY + 2; y++)
                if (Main.tile[x, y].type != ModContent.TileType<BoxTile>() || Main.tile[x, y].frameX < 36) return false;
        return true;
    }

    static void World(Player p, int f)
    {
        int box = ModContent.ItemType<BoxItem>();
        switch (f)
        {
            case 10:
                Report("menu checks ran before the world", menuChecked && logos >= 60, $"logos={logos}");
                Report("AddMusicBox refuses vanilla music, a second box for the same music and vanilla items", BoxItem.Checks == "vanilla-music twice vanilla-item ", BoxItem.Checks);
                Place(p);
                break;
            case 20:
                Wiring.HitWireSingle(boxX, boxY + 1);
                Report("a wire turns the whole 2x2 box on", BoxOn(), $"frameX={Main.tile[boxX, boxY].frameX}");
                break;
            case 60:
                Report("a box that is on plays its music", Wanted == Tone, $"wanted={Wanted} tone={Tone}");
                Wiring.HitWireSingle(boxX + 1, boxY);
                Report("a wire turns it off", !BoxOn() && Main.tile[boxX, boxY].frameX < 36, $"frameX={Main.tile[boxX, boxY].frameX}");
                break;
            case 100:
                Report("a box that is off stops the music", Wanted != Tone, $"wanted={Wanted}");
                p.armor[3].SetDefaults(box, default);
                break;
            case 140:
                Report("an equipped music box plays its music", Wanted == Tone, $"wanted={Wanted}");
                break;
            case 260:
                {
                    p.armor[4].SetDefaults(ItemID.MusicBox, default);
                    bool playing = (int)Call("Terraria.ModLoader.Core.MusicHooks", "Playing")! == Tone;
                    for (int k = 0; k < 4000 && p.armor[4].type == ItemID.MusicBox; k++) Call("Terraria.ModLoader.Core.MusicBoxHooks", "Record", p);
                    Report("an empty music box records the mod music into its box", playing && p.armor[4].type == box, $"playing={playing} type={p.armor[4].type}");
                    p.armor[3].TurnToAir();
                    p.armor[4].TurnToAir();
                    WorldGen.KillTile(boxX, boxY, false, false, true);
                    done = true;
                    break;
                }
        }
    }

    static void Place(Player p)
    {
        boxX = (int)(p.Center.X / 16) + 4;
        int floor = (int)((p.position.Y + p.height) / 16);
        boxY = floor - 2;
        for (int x = boxX - 1; x < boxX + 3; x++)
            for (int y = boxY - 1; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
        bool placed = WorldGen.PlaceObject(boxX, boxY + 1, ModContent.TileType<BoxTile>(), true);
        Report("the music box tile is placed", placed && Main.tile[boxX, boxY].type == ModContent.TileType<BoxTile>(), $"at {boxX},{boxY} placed={placed}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmusicmenu {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
