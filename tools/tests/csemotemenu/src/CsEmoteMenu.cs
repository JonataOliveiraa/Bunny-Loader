using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace CsEmoteMenu;

public sealed class CsEmoteMenuMod : Mod
{
    static int frame, pass, fail, step, waited, person;
    static float scrollBefore;
    static Vector2 half;
    static readonly Dictionary<int, Vector2> drawn = new();

    static ILog Log => ModContent.GetInstance<CsEmoteMenuMod>().Logger;

    static int Emote(string name) => ModContent.Find<ModEmoteBubble>("ExampleMod/" + name).Type;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIEmotesWindow.EmoteDraw += EmoteDraw;
    }

    static void EmoteDraw(On_GUIEmotesWindow.orig_EmoteDraw orig, GUIEmotesWindow self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        drawn[index] = position;
        orig(self, gridLayout, index, position, scale);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csemotemenu {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step();
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish();
        }
    }

    static List<int> Entries(GUIEmotesWindow window)
    {
        var entries = new GameList(window._emoteEntries);
        var ids = new List<int>();
        for (int k = 0; k < entries.Count; k++)
            ids.Add(entries.Get<int>(k));
        return ids;
    }

    static string Pixel(Vector2 ui) => $"{ui.X + half.X:0.0} {ui.Y + half.Y:0.0}";

    static bool PersonBubble()
    {
        var bubbles = new GameDictionary(EmoteBubble.byID);
        for (int id = Math.Max(0, EmoteBubble.NextID - 50); id < EmoteBubble.NextID; id++)
        {
            if (!bubbles.TryGetValue(id, out IntPtr pointer) || pointer == IntPtr.Zero)
                continue;
            var bubble = new EmoteBubble(pointer);
            if (bubble.emote == person && bubble.anchor.entity.Pointer == Main.LocalPlayer.Pointer)
                return true;
        }
        return false;
    }

    static void Step()
    {
        GUIEmotesWindow window = GUIInstance.Active.GUIEmotesWindow;
        DraggableItemGrid_Layout grid = Emotes_Layout.Instance.EmotesGrid;
        switch (step)
        {
            case 0 when frame == 60:
            {
                Main.playerInventory = false;
                person = Emote("ExamplePersonEmote");
                int[] town = { person, Emote("ExampleTravellingMerchantEmote"), Emote("ExampleBoneMerchantEmote") };
                window.SetDisplayingEmotes(true);
                window.SetCategory(GUIEmotesWindow.Category.TownNPCs);
                List<int> ids = Entries(window);
                Report("the town emotes list the mod emotes", town.All(ids.Contains), $"ids={string.Join(",", ids)}");
                drawn.Clear();
                step = 1;
                waited = frame;
                break;
            }
            case 1 when frame == waited + 30:
            {
                List<int> ids = Entries(window);
                var shared = drawn.GroupBy(p => (MathF.Round(p.Value.X), MathF.Round(p.Value.Y))).Where(g => g.Count() > 1).ToList();
                Report("each emote gets its own cell", drawn.Count >= ids.Count && shared.Count == 0, $"drawn={drawn.Count} entries={ids.Count} shared={string.Join(" ", shared.Select(g => string.Join("/", g.Select(p => p.Key))))}");
                int index = ids.IndexOf(person);
                float right = drawn.Where(p => ids[p.Key] < EmoteID.Count).Max(p => p.Value.X);
                Report("the mod emote sits right of the vanilla ones", drawn.TryGetValue(index, out Vector2 at) && at.X > right, $"at={at.X} vanilla={right}");
                half = new Vector2(drawn[3].X - drawn[0].X, drawn[1].Y - drawn[0].Y) / 2f;
                scrollBefore = window.emoteScrollOffset;
                Vector2 from = drawn[ids.Count - 1];
                Log.Info($"csemotemenu SWIPE 1 {Pixel(new Vector2(right, from.Y))} {Pixel(new Vector2(drawn[0].X, from.Y))}");
                step = 2;
                waited = 0;
                break;
            }
            case 2:
                if (window.emoteScrollOffset != scrollBefore && waited > 60 || ++waited > 900)
                {
                    Report("the emote grid scrolls sideways", window.emoteScrollOffset != scrollBefore, $"offset={window.emoteScrollOffset}");
                    waited = Entries(window).IndexOf(person);
                    drawn.Clear();
                    step = 3;
                }
                break;
            case 3 when drawn.ContainsKey(waited):
                Log.Info($"csemotemenu TAP 2 {Pixel(drawn[waited])}");
                step = 4;
                waited = 0;
                break;
            case 4:
                if (PersonBubble() || ++waited > 900)
                {
                    Report("a real tap on the mod emote shows it over the player", PersonBubble());
                    Report("the emote window closes after the tap", !window.DisplayingEmotes);
                    window.SetDisplayingEmotes(true);
                    window.SetCategory(GUIEmotesWindow.Category.Items);
                    Report("a category that fits keeps the game's grid", grid.Horizontal && grid.ItemLineCount == 10, $"horizontal={grid.Horizontal} line={grid.ItemLineCount}");
                    window.SetDisplayingEmotes(false);
                    Finish();
                }
                break;
        }
    }

    static void Finish()
    {
        step = 99;
        Log.Info($"csemotemenu END pass={pass} fail={fail}");
    }
}
