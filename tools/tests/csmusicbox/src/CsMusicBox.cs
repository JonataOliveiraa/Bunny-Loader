using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMusicBox;

public sealed class CsMusicBoxMod : Mod
{
    const int VanillaBox = 139, VanillaBoxStyleFrameY = 36, TogglePeriod = 12;
    const float MaxAudioP99 = 5;

    enum Target { None, Vanilla, Modded }

    sealed record Phase(string Name, Target Box, bool Toggle, bool On, double Seconds);

    static readonly Phase[] Phases =
    {
        new("idle", Target.None, false, false, 12),
        new("vanilla_on", Target.Vanilla, false, true, 8),
        new("vanilla_off", Target.Vanilla, false, false, 8),
        new("vanilla_toggle", Target.Vanilla, true, false, 25),
        new("vanilla_after", Target.Vanilla, false, false, 15),
        new("mod_on", Target.Modded, false, true, 8),
        new("mod_off", Target.Modded, false, false, 8),
        new("mod_toggle", Target.Modded, true, false, 25),
        new("mod_after", Target.Modded, false, false, 10),
    };

    static ILog Log => ModContent.GetInstance<CsMusicBoxMod>().Logger;

    static long lastTick, phaseStart, secondStart;
    static int phase = -1, ticks, boxX, boxY, modBoxX, modBoxY, slot, toggles, secondToggles;
    static bool done, placed, on;
    static float savedVolume = -1;
    static int missed, pass, fail, firstThreads;
    static readonly List<float> phaseFrames = new(), secondFrames = new(), phaseAudio = new(), secondAudio = new();
    static IList? tracks;
    static FieldInfo? fadeField;

    public override void Load()
    {
        On_Main.UpdateAudio += UpdateAudio;
        On_Player.Update += Update;
        On_NPC.SpawnNPC += orig => { };
        On_NPC_Spawner.SpawnNPC += (orig, self) => { };
    }

    static void UpdateAudio(On_Main.orig_UpdateAudio orig, Main self)
    {
        long start = Stopwatch.GetTimestamp();
        if (lastTick != 0 && phase >= 0 && !done)
        {
            float frame = Milliseconds(start - lastTick);
            phaseFrames.Add(frame);
            secondFrames.Add(frame);
        }
        lastTick = start;
        orig(self);
        if (phase < 0 || done) return;
        float audio = Milliseconds(Stopwatch.GetTimestamp() - start);
        phaseAudio.Add(audio);
        secondAudio.Add(audio);
    }

    static float Milliseconds(long stopwatchTicks) => (float)(stopwatchTicks * 1000.0 / Stopwatch.Frequency);

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (i == Main.myPlayer && !Main.gameMenu)
        {
            self.creativeGodMode = true;
            self.statLife = self.statLifeMax2;
            self.breath = self.breathMax;
        }
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        try { Step(self); }
        catch (Exception e)
        {
            Log.Info($"csmusicbox threw {e}");
            Finish();
        }
    }

    static void Step(Player player)
    {
        if (++ticks < 180) return;
        if (!placed) Place(player);
        long now = Stopwatch.GetTimestamp();
        if (phase < 0 || (now - phaseStart) / (double)Stopwatch.Frequency >= Phases[phase].Seconds)
        {
            if (phase >= 0) ReportPhase(Phases[phase]);
            if (++phase >= Phases.Length)
            {
                Finish();
                return;
            }
            phaseStart = secondStart = now;
            phaseFrames.Clear();
            phaseAudio.Clear();
            secondFrames.Clear();
            secondAudio.Clear();
            toggles = secondToggles = 0;
            SetBoxes(Phases[phase].Box, Phases[phase].On);
        }
        Phase current = Phases[phase];
        if (current.Toggle && ticks % TogglePeriod == 0)
        {
            Interact(player, current.Box);
            toggles++;
            secondToggles++;
        }
        if ((now - secondStart) / (double)Stopwatch.Frequency >= 1)
        {
            ReportSecond(current);
            secondStart = now;
        }
    }

    static void Interact(Player player, Target target)
    {
        int x = target == Target.Vanilla ? boxX : modBoxX, y = target == Target.Vanilla ? boxY : modBoxY;
        player.tileInteractAttempted = true;
        player.releaseUseTile = true;
        player.TileInteractionsUse(x, y);
        on = !on;
        if (Main.tile[x, y].frameX >= 36 != on) missed++;
    }

    static void Place(Player player)
    {
        placed = true;
        if (Main.musicVolume < 0.2f)
        {
            savedVolume = Main.musicVolume;
            Main.musicVolume = 0.5f;
        }
        int x = (int)(player.Center.X / 16), y = (int)(player.Center.Y / 16);
        boxX = x + 3;
        boxY = y - 8;
        modBoxX = x - 4;
        modBoxY = y - 8;
        int modType = ModContent.Find<ModTile>("ExampleMod/ExampleMusicBoxTile").Type;
        PutBox(boxX, boxY, VanillaBox, VanillaBoxStyleFrameY);
        PutBox(modBoxX, modBoxY, modType, 0);
        slot = MusicLoader.GetMusicSlot("ExampleMod/Assets/Music/MysteriousMystery");
        tracks = typeof(MusicLoader).GetField("Tracks", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IList;
        if (tracks?.Count > 0) fadeField = tracks[0]!.GetType().GetField("Fade");
        Log.Info($"csmusicbox ready volume={Main.musicVolume} slot={slot} modTile={modType} tracks={tracks?.Count} cur={Main.curMusic} new={Main.newMusic}");
    }

    static void PutBox(int x, int y, int type, int frameY)
    {
        for (int a = 0; a < 2; a++)
        {
            for (int b = 0; b < 2; b++)
            {
                Tile cell = Main.tile[x + a, y + b];
                cell.active(true);
                cell.type = (ushort)type;
                cell.frameX = (short)(a * 18);
                cell.frameY = (short)(frameY + b * 18);
            }
        }
    }

    static void SetBoxes(Target target, bool wantOn)
    {
        on = wantOn;
        Flip(boxX, boxY, target == Target.Vanilla && wantOn);
        Flip(modBoxX, modBoxY, target == Target.Modded && wantOn);
    }

    static void Flip(int x, int y, bool wantOn)
    {
        for (int a = 0; a < 2; a++)
        {
            for (int b = 0; b < 2; b++)
            {
                Tile cell = Main.tile[x + a, y + b];
                cell.frameX = (short)(a * 18 + (wantOn ? 36 : 0));
            }
        }
    }

    static string Fades()
    {
        if (tracks == null || fadeField == null) return "-";
        var parts = new List<string>();
        foreach (object? track in tracks) parts.Add(((float)fadeField.GetValue(track)!).ToString("0.00"));
        return string.Join("/", parts);
    }

    static (string Threads, string Rss) Process()
    {
        string threads = "?", rss = "?";
        foreach (string line in File.ReadAllLines("/proc/self/status"))
        {
            if (line.StartsWith("Threads:", StringComparison.Ordinal)) threads = line[8..].Trim();
            else if (line.StartsWith("VmRSS:", StringComparison.Ordinal)) rss = line[6..].Trim();
        }
        return (threads, rss);
    }

    static string Stats(List<float> values)
    {
        if (values.Count == 0) return "n=0";
        var sorted = new List<float>(values);
        sorted.Sort();
        double sum = 0;
        foreach (float value in sorted) sum += value;
        float At(double q) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];
        return $"n={sorted.Count} avg={sum / sorted.Count:0.00} p50={At(0.5):0.00} p95={At(0.95):0.00} p99={At(0.99):0.00} max={sorted[^1]:0.00}";
    }

    static void ReportSecond(Phase current)
    {
        var process = Process();
        Vector2 at = Main.LocalPlayer.Center;
        Log.Info($"csmusicbox SEC {current.Name} toggles={secondToggles} frame[{Stats(secondFrames)}] audio[{Stats(secondAudio)}] threads={process.Threads} rss={process.Rss} new={Main.newMusic}/{Main.curMusic} box={Main.SceneMetrics.ActiveMusicBox} fade={Fades()} mfx={Main.tile[modBoxX, modBoxY].frameX} vfx={Main.tile[boxX, boxY].frameX} at={(int)(at.X / 16)},{(int)(at.Y / 16)}");
        secondFrames.Clear();
        secondAudio.Clear();
        secondToggles = 0;
    }

    static void ReportPhase(Phase current)
    {
        var process = Process();
        bool playing = MusicLoader.IsMusicPlaying(slot);
        Log.Info($"csmusicbox PHASE {current.Name} toggles={toggles} frame[{Stats(phaseFrames)}] audio[{Stats(phaseAudio)}] threads={process.Threads} rss={process.Rss} fade={Fades()} playing={playing}");
        int threads = int.TryParse(process.Threads, out int count) ? count : 0;
        switch (current.Name)
        {
            case "idle":
                firstThreads = threads;
                break;
            case "mod_on":
                Report("the mod music box plays its track", playing, $"playing={playing}");
                break;
            case "mod_off":
                Report("turning the mod music box off stops the track", !playing, $"playing={playing}");
                break;
            case "vanilla_toggle":
            case "mod_toggle":
                Report($"{current.Name}: every interaction switches the box", missed == 0 && toggles > 0, $"toggles={toggles} missed={missed}");
                float p99 = Percentile(phaseAudio, 0.99);
                Report($"{current.Name}: UpdateAudio stays short on the game thread", p99 < MaxAudioP99, $"p99={p99:0.00} ms, limit {MaxAudioP99} ms");
                missed = 0;
                break;
            case "mod_after":
                Report("toggling leaves no thread behind", threads - firstThreads <= 2, $"first={firstThreads} now={threads}");
                Report("the mod track stays stopped after toggling", !playing, $"playing={playing}");
                break;
        }
    }

    static float Percentile(List<float> values, double q)
    {
        if (values.Count == 0) return 0;
        var sorted = new List<float>(values);
        sorted.Sort();
        return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmusicbox {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Finish()
    {
        done = true;
        if (placed) SetBoxes(Target.None, false);
        if (savedVolume >= 0) Main.musicVolume = savedVolume;
        Log.Info($"csmusicbox END pass={pass} fail={fail}");
    }
}
