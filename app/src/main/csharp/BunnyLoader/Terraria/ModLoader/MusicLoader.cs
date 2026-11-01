using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class MusicLoader
{
    internal sealed class Track
    {
        public int Id;
        public string File = "";
        public float Fade;
        public float Sent = -1;
    }

    static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };
    static readonly Dictionary<string, int> Slots = new(StringComparer.Ordinal);
    static readonly Dictionary<(Mod, string), int> ByPath = new();
    internal static readonly List<Track> Tracks = new();
    internal static readonly Dictionary<int, int> musicToItem = new();
    internal static readonly Dictionary<int, int> itemToMusic = new();
    internal static readonly Dictionary<int, Dictionary<int, int>> tileToMusic = new();

    public static int MusicCount => MusicID.Count + Tracks.Count;

    internal static Track? TrackOf(int slot)
    {
        int index = slot - MusicID.Count;
        return (uint)index < (uint)Tracks.Count ? Tracks[index] : null;
    }

    public static int GetMusicSlot(Mod mod, string musicPath)
    {
        lock (Slots)
            if (ByPath.TryGetValue((mod, musicPath), out int cached)) return cached;
        int slot = Register(mod, musicPath);
        lock (Slots) ByPath[(mod, musicPath)] = slot;
        return slot;
    }

    static int Register(Mod mod, string musicPath)
    {
        string? file = Resolve(mod, musicPath);
        if (file == null)
        {
            Log.Warn($"MusicLoader: '{musicPath}' not found in {mod.Name} ({string.Join(", ", Extensions)})");
            return 0;
        }
        lock (Slots)
        {
            if (Slots.TryGetValue(file, out int known)) return known;
            byte* path = Host.Utf8(file);
            int id;
            try { id = Host.Native()->MusicRegister(path); }
            finally { Host.FreeUtf8(path); }
            if (id <= 0)
            {
                Log.Error($"MusicLoader: Android refused {file}");
                return 0;
            }
            int slot = MusicID.Count + Tracks.Count;
            Tracks.Add(new Track { Id = id, File = file });
            Slots[file] = slot;
            MusicHooks.Install();
            return slot;
        }
    }

    public static int GetMusicSlot(string musicPath)
    {
        Split(musicPath, out Mod? mod, out string path);
        return mod == null ? 0 : GetMusicSlot(mod, path);
    }

    public static bool MusicExists(Mod mod, string musicPath) => Resolve(mod, musicPath) != null;

    public static bool MusicExists(string musicPath)
    {
        Split(musicPath, out Mod? mod, out string path);
        return mod != null && MusicExists(mod, path);
    }

    public static void AddMusic(Mod mod, string musicPath)
    {
        if (GetMusicSlot(mod, musicPath) == 0) throw new FileNotFoundException($"MusicLoader.AddMusic: '{musicPath}' not found in {mod.Name}");
    }

    public static void AddMusicBox(Mod mod, int musicSlot, int itemType, int tileType, int tileFrameY = 0)
    {
        if (musicSlot < MusicID.Count)
        {
            if (musicSlot == 0) throw new ArgumentOutOfRangeException(nameof(musicSlot), "An invalid music audio file was provided: GetMusicSlot takes the path without the extension (.ogg, .wav or .mp3)");
            throw new ArgumentOutOfRangeException(nameof(musicSlot), $"Cannot assign music box to vanilla music ID {musicSlot}");
        }
        if (musicSlot >= MusicCount) throw new ArgumentOutOfRangeException(nameof(musicSlot), $"Music ID {musicSlot} does not exist");
        if (itemType < ItemID.Count) throw new ArgumentOutOfRangeException(nameof(itemType), $"Cannot assign music box to vanilla item ID {itemType}");
        if (ItemLoader.Get(itemType) == null) throw new ArgumentOutOfRangeException(nameof(itemType), $"Item ID {itemType} does not exist");
        if (tileType < TileID.Count) throw new ArgumentOutOfRangeException(nameof(tileType), $"Cannot assign music box to vanilla tile ID {tileType}");
        if (TileLoader.GetTile(tileType) == null) throw new ArgumentOutOfRangeException(nameof(tileType), $"Tile ID {tileType} does not exist");
        lock (Slots)
        {
            if (musicToItem.ContainsKey(musicSlot)) throw new ArgumentException($"Music ID {musicSlot} has already been assigned a music box");
            if (itemToMusic.ContainsKey(itemType)) throw new ArgumentException($"Item ID {itemType} has already been assigned a music");
            if (!tileToMusic.TryGetValue(tileType, out Dictionary<int, int>? frames)) tileToMusic[tileType] = frames = new Dictionary<int, int>();
            if (frames.ContainsKey(tileFrameY)) throw new ArgumentException($"Y-frame {tileFrameY} of tile type {tileType} has already been assigned a music");
            if (tileFrameY % 36 != 0) throw new ArgumentException("Y-frame must be divisible by 36");
            musicToItem[musicSlot] = itemType;
            itemToMusic[itemType] = musicSlot;
            frames[tileFrameY] = musicSlot;
        }
        MusicBoxHooks.Install(tileType);
    }

    internal static int TileMusic(int type, int frameY)
    {
        lock (Slots)
            return tileToMusic.TryGetValue(type, out Dictionary<int, int>? frames) && frames.TryGetValue(frameY - frameY % 36, out int slot) ? slot : 0;
    }

    internal static bool IsModMusicBox(int type)
    {
        lock (Slots) return tileToMusic.ContainsKey(type);
    }

    public static bool IsMusicPlaying(int slot) => TrackOf(slot) is { } track && Host.Native()->MusicState(track.Id) == 2;

    internal static void SetVolume(Track track, float volume) => Host.Native()->MusicVolume(track.Id, volume);

    static void Split(string musicPath, out Mod? mod, out string path)
    {
        int slash = musicPath.IndexOf('/');
        mod = slash > 0 && ModLoader.TryGetMod(musicPath[..slash], out Mod found) ? found : null;
        path = slash > 0 ? musicPath[(slash + 1)..] : musicPath;
        if (mod == null) Log.Warn($"MusicLoader: no mod in '{musicPath}' (expected ModName/Path)");
    }

    static string? Resolve(Mod mod, string musicPath)
    {
        string relative = musicPath.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith(mod.Name + "/", StringComparison.Ordinal)) relative = relative[(mod.Name.Length + 1)..];
        bool hasExtension = Path.HasExtension(relative) && Array.IndexOf(Extensions, Path.GetExtension(relative).ToLowerInvariant()) >= 0;
        if (hasExtension) return Existing(mod.ResolveFile(relative));
        foreach (string extension in Extensions)
        {
            if (Existing(mod.ResolveFile(relative + extension)) is { } file) return file;
        }
        return null;
    }

    static string? Existing(string? file) => file != null && File.Exists(file) ? file : null;
}
