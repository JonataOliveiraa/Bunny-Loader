using System;
using BunnyLoader.Interop;
using CsEmote.Content;
using log4net;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsEmote;

public static class Checks
{
    static int pass, fail, frames, talker = -1, first = -1, second = -1, menuMark;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsEmoteMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        talker = first = second = -1;
        done = false;
    }

    static EmoteBubble Bubble(int id) =>
        new GameDictionary(EmoteBubble.byID).TryGetValue(id, out IntPtr pointer) ? new EmoteBubble(pointer) : default;

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) Spawn(p);
            if (frames == 150) CheckBubbles();
            if (frames == 160) EmoteBubble.NewBubble(EmoteID.EmotionLove, WorldUIAnchor.New(p), 300);
            if (frames == 220) CheckVanilla();
            if (frames == 230) OpenMenu();
            if (frames == 300) CheckMenu();
            if (frames == 310) Talk(p);
            if (frames == 700) Close();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            if ((uint)talker < 200u) Main.npc[talker].active = false;
            Log.Info($"csemote END pass={pass} fail={fail}");
        }
    }

    static void Spawn(Player p)
    {
        int type = ModContent.EmoteBubbleType<TestEmote>();
        first = EmoteBubble.NewBubble(type, WorldUIAnchor.New(p), 600);
        second = EmoteBubble.NewBubble(type, WorldUIAnchor.New(p.Center + new Microsoft.Xna.Framework.Vector2(80f, 0f)), 600);
        EmoteBubble bubble = Bubble(first);
        Report("a mod emote spawns with its type above EmoteID.Count", type >= EmoteID.Count && !bubble.IsNull && bubble.emote == type, $"type={type} id={first}");
    }

    static void CheckBubbles()
    {
        EmoteBubble a = Bubble(first), b = Bubble(second);
        var own = a.ModEmoteBubble as TestEmote;
        var other = b.ModEmoteBubble as TestEmote;
        Report("OnSpawn runs once per bubble on its own instance",
            TestEmote.Spawns == 2 && own != null && other != null && own != other && own.SpawnedOn == a.Pointer && other.SpawnedOn == b.Pointer,
            $"spawns={TestEmote.Spawns} distinct={own != other}");
        Report("UpdateFrame, PreDraw and PostDraw run and the bubble animates its two frames",
            TestEmote.Updates > 0 && TestEmote.Pres > 0 && TestEmote.Posts > 0 && TestEmote.Frames.Count == 2,
            $"updates={TestEmote.Updates} pre={TestEmote.Pres} post={TestEmote.Posts} frames={string.Join(",", TestEmote.Frames)}");
    }

    static void CheckVanilla()
    {
        Report("GlobalEmoteBubble runs on a vanilla emote", TestGlobalEmote.VanillaSpawns == 1 && TestGlobalEmote.VanillaDraws > 0,
            $"spawns={TestGlobalEmote.VanillaSpawns} draws={TestGlobalEmote.VanillaDraws}");
    }

    static void OpenMenu()
    {
        GUIEmotesWindow window = GUIInstance.Active.GUIEmotesWindow;
        window.SetDisplayingEmotes(true);
        window.SetCategory(GUIEmotesWindow.Category.General);
        menuMark = TestEmote.MenuFrames;
    }

    static void CheckMenu()
    {
        GUIEmotesWindow window = GUIInstance.Active.GUIEmotesWindow;
        var entries = new GameList(window._emoteEntries);
        bool listed = false, locked = false;
        int test = ModContent.EmoteBubbleType<TestEmote>(), hidden = ModContent.EmoteBubbleType<LockedEmote>();
        for (int i = 0; i < entries.Count; i++)
        {
            int emote = entries.Get<int>(i);
            listed |= emote == test;
            locked |= emote == hidden;
        }
        Report("the emote menu lists unlocked mod emotes in their category", listed && !locked, $"listed={listed} locked={locked} entries={entries.Count}");
        Report("the emote menu draws the mod icon", TestEmote.MenuFrames > menuMark, $"menu frames={TestEmote.MenuFrames - menuMark}");
    }

    static void Talk(Player p)
    {
        talker = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X - 80, (int)p.Center.Y - 30, ModContent.NPCType<TalkerNpc>());
        int id = EmoteBubble.NewBubbleNPC(WorldUIAnchor.New(Main.npc[talker]), 300, WorldUIAnchor.New(p));
        EmoteBubble bubble = Bubble(id);
        int test = ModContent.EmoteBubbleType<TestEmote>();
        Report("ModNPC.PickEmote picks a mod emote", !bubble.IsNull && bubble.emote == test, $"emote={(bubble.IsNull ? -99 : bubble.emote)}");
        int face = NPCID.Sets.FaceEmote.Length > ModContent.NPCType<TalkerNpc>() ? NPCID.Sets.FaceEmote[ModContent.NPCType<TalkerNpc>()] : -1;
        Report("NPCID.Sets.FaceEmote accepts a mod emote for a mod NPC", TalkerNpc.FaceEmoteError == "" && face == test, $"error='{TalkerNpc.FaceEmoteError}' face={face}");
    }

    static void Close()
    {
        GUIInstance.Active.GUIEmotesWindow.SetDisplayingEmotes(false);
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csemote {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
