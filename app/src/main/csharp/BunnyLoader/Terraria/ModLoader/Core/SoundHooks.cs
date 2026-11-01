using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;

namespace Terraria.ModLoader.Core;

internal static unsafe class SoundHooks
{
    const int MarkerId = 1000;
    const int GroupBase = 1 << 20;
    static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

    sealed class Sound
    {
        public int[] Ids = Array.Empty<int>();
        public float Volume, Pitch, PitchVariance;
        public int MaxInstances;
        public bool IgnoreNew;
        public SoundType Type;
        public int Group;
        public GameHandle? Marker;
    }

    static readonly List<Sound> Sounds = new();
    static readonly Dictionary<string, int> ByKey = new(StringComparer.Ordinal);
    static readonly Dictionary<string, int> Files = new(StringComparer.Ordinal);
    static readonly Random Random = new();

    static int Index(in SoundStyle style)
    {
        string path = style.SoundPath ?? "";
        string key = $"{path}|{string.Join(",", style.Variants.ToArray())}|{style.Volume}|{style.Pitch}|{style.PitchVariance}|{style.MaxInstances}|{style.SoundLimitBehavior}|{style.Type}";
        lock (Sounds)
        {
            if (ByKey.TryGetValue(key, out int known)) return known;
            var sound = new Sound
            {
                Volume = style.Volume,
                Pitch = style.Pitch,
                PitchVariance = style.PitchVariance,
                MaxInstances = Math.Max(0, style.MaxInstances),
                IgnoreNew = style.SoundLimitBehavior == SoundLimitBehavior.IgnoreNew,
                Type = style.Type,
                Group = GroupBase + Sounds.Count,
            };
            var ids = new List<int>();
            if (style.Variants.IsEmpty) AddFile(ids, path);
            else
                foreach (int variant in style.Variants) AddFile(ids, path + variant);
            sound.Ids = ids.ToArray();
            int index = Sounds.Count;
            Sounds.Add(sound);
            ByKey[key] = index;
            ItemHooks.Once("Sounds", () => On_LegacySoundPlayer.PlaySound += PlaySound);
            return index;
        }
    }

    static void AddFile(List<int> ids, string path)
    {
        if (Files.TryGetValue(path, out int known))
        {
            if (known > 0) ids.Add(known);
            return;
        }
        int id = 0;
        string? file = Resolve(path);
        if (file == null) Log.Warn($"SoundStyle: '{path}' not found ({string.Join(", ", Extensions)})");
        else
        {
            byte* utf8 = Host.Utf8(file);
            try { id = Host.Native()->SoundLoad(utf8); }
            finally { Host.FreeUtf8(utf8); }
            if (id <= 0) Log.Error($"SoundStyle: Android refused {file}");
        }
        Files[path] = id;
        if (id > 0) ids.Add(id);
    }

    static string? Resolve(string path)
    {
        (string modName, string relative) = ModContent.SplitName(path);
        if (Host.FindMod(modName) is not Mod mod) return null;
        foreach (string extension in Extensions)
        {
            if (mod.ResolveFile(relative + extension) is string file && File.Exists(file)) return file;
        }
        return mod.ResolveFile(relative) is string direct && File.Exists(direct) ? direct : null;
    }

    internal static LegacySoundStyle Marker(in SoundStyle style)
    {
        int index = Index(style);
        lock (Sounds)
        {
            Sound sound = Sounds[index];
            sound.Marker ??= new GameHandle(LegacySoundStyle.New(MarkerId, index).Pointer);
            return new LegacySoundStyle(sound.Marker.Target);
        }
    }

    internal static int Play(in SoundStyle style, int x, int y) => Play(Index(style), x, y, 1f, 0f);

    static int Play(int index, int x, int y, float volumeScale, float pitchOffset)
    {
        Sound sound;
        lock (Sounds)
        {
            if ((uint)index >= (uint)Sounds.Count) return 0;
            sound = Sounds[index];
        }
        if (sound.Ids.Length == 0) return 0;
        float volume = 1f, pan = 0f;
        if (x != -1 && y != -1)
        {
            float attenuation = LegacySoundPlayer.SoundAttenuationDistance;
            if (!(attenuation > 0)) attenuation = 2500f;
            var screen = Main.screenPosition;
            float halfWidth = Main.screenWidth / 2f;
            float cx = screen.X + halfWidth, cy = screen.Y + Main.screenHeight / 2f;
            float distance = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (distance >= attenuation) return 0;
            pan = Math.Clamp((x - cx) / halfWidth, -1f, 1f);
            volume = 1f - distance / attenuation;
        }
        float master = sound.Type switch
        {
            SoundType.Ambient => Main.ambientVolume,
            SoundType.Music => Main.musicVolume,
            _ => Main.soundVolume,
        };
        volume *= sound.Volume * volumeScale * master;
        if (!(volume > 0)) return 0;
        int id = sound.Ids.Length == 1 ? sound.Ids[0] : sound.Ids[Random.Next(sound.Ids.Length)];
        float rate = MathF.Pow(2f, sound.Pitch + ((float)Random.NextDouble() - 0.5f) * sound.PitchVariance + pitchOffset);
        return Host.Native()->SoundEnqueue(sound.Group, id, volume * Math.Min(1f, 1f - pan), volume * Math.Min(1f, 1f + pan), rate,
            sound.MaxInstances, sound.IgnoreNew ? 1 : 0);
    }

    static SoundEffectInstance PlaySound(On_LegacySoundPlayer.orig_PlaySound orig, LegacySoundPlayer self, int type, int x, int y, int Style, float volumeScale, float pitchOffset)
    {
        if (type != MarkerId) return orig(self, type, x, y, Style, volumeScale, pitchOffset);
        try { Play(Style, x, y, volumeScale, pitchOffset); }
        catch (Exception e) { ItemHooks.Report("SoundStyle (play)", e); }
        return default;
    }
}
