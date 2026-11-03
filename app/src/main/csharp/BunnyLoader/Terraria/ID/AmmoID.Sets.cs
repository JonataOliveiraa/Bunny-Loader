using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public static partial class AmmoID
{
    public readonly partial struct Sets
    {
        static readonly object Gate = new();

        static int[] specificLauncherAmmoProjectileFallback = Array.Empty<int>();

        public static int[] SpecificLauncherAmmoProjectileFallback
        {
            get
            {
                int count = ItemID.Sets.BossBag.Length;
                lock (Gate)
                {
                    if (specificLauncherAmmoProjectileFallback.Length >= count) return specificLauncherAmmoProjectileFallback;
                    int old = specificLauncherAmmoProjectileFallback.Length;
                    Array.Resize(ref specificLauncherAmmoProjectileFallback, count);
                    Array.Fill(specificLauncherAmmoProjectileFallback, -1, old, count - old);
                    LauncherAmmoHooks.Install();
                    return specificLauncherAmmoProjectileFallback;
                }
            }
        }

        public static LauncherAmmoMatches SpecificLauncherAmmoProjectileMatches { get; } = new();

        public sealed class LauncherAmmoMatches
        {
            readonly Dictionary<int, Dictionary<int, int>> own = new();

            static GameDictionary Game => new(SpecificLauncherAmmoProjectileMatches_);

            public int Count
            {
                get
                {
                    lock (own)
                    {
                        int count = own.Count;
                        foreach (int launcher in own.Keys)
                            if (Game.ContainsKey(launcher)) count--;
                        return count + Game.Count;
                    }
                }
            }

            public Dictionary<int, int> this[int launcher]
            {
                get => TryGetValue(launcher, out Dictionary<int, int>? matches) ? matches : throw new KeyNotFoundException($"SpecificLauncherAmmoProjectileMatches has no launcher {launcher}");
                set
                {
                    ArgumentNullException.ThrowIfNull(value);
                    lock (own) own[launcher] = value;
                    LauncherAmmoHooks.Install();
                }
            }

            public void Add(int launcher, Dictionary<int, int> matches)
            {
                ArgumentNullException.ThrowIfNull(matches);
                lock (own)
                {
                    if (own.ContainsKey(launcher) || Game.ContainsKey(launcher))
                        throw new ArgumentException($"SpecificLauncherAmmoProjectileMatches already has launcher {launcher}");
                    own.Add(launcher, matches);
                }
                LauncherAmmoHooks.Install();
            }

            public bool ContainsKey(int launcher)
            {
                lock (own) return own.ContainsKey(launcher) || Game.ContainsKey(launcher);
            }

            public bool TryGetValue(int launcher, out Dictionary<int, int> matches)
            {
                lock (own)
                {
                    if (own.TryGetValue(launcher, out matches!)) return true;
                    if (!Game.TryGetValue(launcher, out IntPtr table) || table == IntPtr.Zero) return false;
                    matches = new Dictionary<int, int>();
                    foreach (KeyValuePair<int, int> pair in new GameDictionary(table).IntEntries()) matches[pair.Key] = pair.Value;
                    own[launcher] = matches;
                }
                LauncherAmmoHooks.Install();
                return true;
            }

            public bool Remove(int launcher)
            {
                bool removed;
                lock (own)
                {
                    removed = own.Remove(launcher);
                    removed |= Game.Remove(launcher);
                }
                return removed;
            }

            internal bool TryOwn(int launcher, out Dictionary<int, int> matches)
            {
                lock (own) return own.TryGetValue(launcher, out matches!);
            }
        }
    }
}
