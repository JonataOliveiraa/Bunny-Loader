using System;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class MusicHooks
{
    const float FadeStep = 0.005f, Audible = 0.25f;
    const int ChooseEvery = 10;

    static int wanted = -1, chooseIn;
    static bool chosenOtherworld;
    static float? savedSilenceFade;

    internal static int Wanted => wanted;

    internal static void Install() => ItemHooks.Once("Music", () =>
    {
        On_Main.UpdateAudio_DecideOnNewMusic += DecideOnNewMusic;
        On_Main.UpdateAudio_DecideOnTOWMusic += DecideOnTOWMusic;
        On_Main.UpdateAudio += UpdateAudio;
    });

    static void DecideOnNewMusic(On_Main.orig_UpdateAudio_DecideOnNewMusic orig, Main self)
    {
        orig(self);
        Decide(false);
    }

    static void DecideOnTOWMusic(On_Main.orig_UpdateAudio_DecideOnTOWMusic orig, Main self)
    {
        orig(self);
        Decide(true);
    }

    static void UpdateAudio(On_Main.orig_UpdateAudio orig, Main self)
    {
        orig(self);
        try { Step(); }
        catch (Exception e) { ItemHooks.Report("Music (fade)", e); }
    }

    static int Choose(bool otherworld)
    {
        if (Main.gameMenu) return -1;
        if (Main.SceneMetrics.ActiveMusicBox >= 0) return -1;
        int music = -1;
        SceneEffectPriority priority = SceneEffectPriority.None;
        FromNpcs(ref music, ref priority);
        if (SceneEffectLoader.Count > 0) SceneEffectLoader.UpdateMusic(ref music, ref priority);
        if (music < 0 || priority < SceneEffectPriority.BiomeLow) return -1;
        return priority >= VanillaMusic.Tier(otherworld) ? music : -1;
    }

    static void FromNpcs(ref int music, ref SceneEffectPriority priority)
    {
        var screen = Main.screenPosition;
        const float margin = 5000;
        float x0 = screen.X - margin, x1 = screen.X + Main.screenWidth + margin;
        float y0 = screen.Y - margin, y1 = screen.Y + Main.screenHeight + margin;
        var npcs = Main.npc;
        for (int i = 0; i < npcs.Length; i++)
        {
            NPC npc = npcs[i];
            if (!npc.active || npc.type < NPCID.Count || npc.ModNPC is not { } modNpc || modNpc.Music < 0) continue;
            var center = npc.Center;
            if (center.X < x0 || center.X > x1 || center.Y < y0 || center.Y > y1) continue;
            if (music < 0 || modNpc.SceneEffectPriority > priority)
            {
                music = modNpc.Music;
                priority = modNpc.SceneEffectPriority;
            }
        }
    }

    static void SilenceGame(bool on)
    {
        var fade = Main.musicFade;
        if (on)
        {
            savedSilenceFade ??= fade[0];
            fade[0] = 1;
            Main.newMusic = 0;
        }
        else if (savedSilenceFade is float saved)
        {
            fade[0] = saved;
            savedSilenceFade = null;
        }
    }

    static void Decide(bool otherworld)
    {
        try
        {
            if (--chooseIn <= 0 || chosenOtherworld != otherworld)
            {
                wanted = Choose(otherworld);
                chooseIn = ChooseEvery;
                chosenOtherworld = otherworld;
            }
            MusicLoader.Track? track = MusicLoader.TrackOf(wanted);
            if (track != null && track.Fade > Audible)
            {
                SilenceGame(true);
                return;
            }
            SilenceGame(false);
            if (track != null) Main.newMusic = Main.curMusic;
            else if (wanted >= 0) Main.newMusic = wanted;
        }
        catch (Exception e) { ItemHooks.Report("Music (choice)", e); }
    }

    static void Step()
    {
        if (MusicLoader.Tracks.Count == 0) return;
        MusicLoader.Track? main = Main.gameMenu ? null : MusicLoader.TrackOf(wanted);
        int current = Main.curMusic;
        bool mainAudible = main != null ? main.Fade > Audible : current <= 0 || Main.musicFade[current] > Audible;
        float volume = Main.musicVolume;
        foreach (MusicLoader.Track track in MusicLoader.Tracks)
        {
            if (track == main) track.Fade = Math.Min(1, track.Fade + FadeStep);
            else if (mainAudible) track.Fade = Math.Max(0, track.Fade - FadeStep);
            float v = track.Fade * volume;
            if (v == track.Sent) continue;
            MusicLoader.SetVolume(track, v);
            track.Sent = v;
        }
    }
}
