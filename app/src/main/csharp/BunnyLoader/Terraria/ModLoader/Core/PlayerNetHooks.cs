using System;
using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.ModLoader.Core;

internal static class PlayerNetHooks
{
    sealed class Entry
    {
        public Player Player;
        public GameHandle Pin = null!;
    }

    static readonly HashSet<IntPtr> Copying = new();
    static readonly Dictionary<int, Entry> Connected = new();
    static readonly HashSet<int> Disconnected = new();

    static Player baseline;
    static GameHandle? baselinePin;
    static IntPtr source;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.CopyClientState)) || has(nameof(ModPlayer.SendClientChanges)))
            ItemHooks.Once("PlayerClientState", () =>
            {
                On_Player.clientClone_Player += ClientCloneInto;
                On_Player.clientClone += ClientClone;
                On_Main.UpdateClient += UpdateClient;
            });
        if (has(nameof(ModPlayer.SyncPlayer)) || has(nameof(ModPlayer.PlayerConnect)) || has(nameof(ModPlayer.PlayerDisconnect)))
            ItemHooks.Once("PlayerNetLifecycle", () =>
            {
                On_NetMessage.SyncOnePlayer += SyncOnePlayer;
                On_Player_Hooks.PlayerConnect += PlayerConnect;
                On_Player_Hooks.EnterWorld += EnterWorld;
                On_Player_Hooks.PlayerDisconnect += PlayerDisconnect;
                On_NetMessage.SyncDisconnectedPlayer += SyncDisconnectedPlayer;
                On_Player.Update += Update;
            });
    }

    static GameObject ClientCloneInto(On_Player.orig_clientClone_Player orig, Player self, Player clonePlayer)
    {
        if (!Copying.Add(self.Pointer)) return orig(self, clonePlayer);
        try
        {
            GameObject result = orig(self, clonePlayer);
            Copy(self, clonePlayer);
            return result;
        }
        finally { Copying.Remove(self.Pointer); }
    }

    static Player ClientClone(On_Player.orig_clientClone orig, Player self)
    {
        if (!Copying.Add(self.Pointer)) return orig(self);
        try
        {
            Player clone = orig(self);
            Copy(self, clone);
            return clone;
        }
        finally { Copying.Remove(self.Pointer); }
    }

    static void Copy(Player player, Player clone)
    {
        if (clone.IsNull || clone == player) return;
        ModPlayer[] hooked = PlayerLoader.Hooked(nameof(ModPlayer.CopyClientState));
        if (hooked.Length == 0) return;
        ModPlayer[] copies = PlayerLoader.Of(clone);
        PlayerLoader.Each(player, nameof(ModPlayer.CopyClientState), copies, static (m, c) => m.CopyClientState(c[m.Index]));
    }

    static void UpdateClient(On_Main.orig_UpdateClient orig)
    {
        if (Main.gameMenu || (Main.netMode & 1) == 0)
        {
            Forget();
            orig();
            return;
        }
        Player player = Main.player[Main.myPlayer];
        if (player.IsNull)
        {
            orig();
            return;
        }
        if (source == player.Pointer && !baseline.IsNull)
        {
            ModPlayer[] previous = PlayerLoader.Of(baseline);
            PlayerLoader.Each(player, nameof(ModPlayer.SendClientChanges), previous, static (m, p) => m.SendClientChanges(p[m.Index]));
        }
        orig();
        try
        {
            if (source != player.Pointer || baseline.IsNull)
            {
                Forget();
                baseline = player.clientClone();
                baselinePin = new GameHandle(baseline.Pointer);
            }
            else player.clientClone(baseline);
            source = player.Pointer;
        }
        catch (Exception e) { ItemHooks.Report("SendClientChanges (client copy)", e); }
    }

    static void Forget()
    {
        baselinePin?.Dispose();
        baselinePin = null;
        baseline = default;
        source = IntPtr.Zero;
    }

    static void SyncOnePlayer(On_NetMessage.orig_SyncOnePlayer orig, int plr, int toWho, int fromWho)
    {
        orig(plr, toWho, fromWho);
        if ((uint)plr >= 255u) return;
        Player player = Main.player[plr];
        if (player.IsNull) return;
        if (Connected.TryGetValue(plr, out Entry? previous) && previous.Player != player) Disconnect(plr);
        bool fresh = !Connected.ContainsKey(plr);
        PlayerLoader.Each(player, nameof(ModPlayer.SyncPlayer), (toWho, fromWho, fresh), static (m, s) => m.SyncPlayer(s.toWho, s.fromWho, s.fresh));
        if (player.active) Connect(plr, player);
    }

    static void PlayerConnect(On_Player_Hooks.orig_PlayerConnect orig, int playerIndex)
    {
        orig(playerIndex);
        if ((uint)playerIndex >= 255u) return;
        Player player = Main.player[playerIndex];
        if (player.IsNull) return;
        if (Connected.TryGetValue(playerIndex, out Entry? previous) && previous.Player != player) Disconnect(playerIndex);
        Connect(playerIndex, player);
        PlayerLoader.Each(player, nameof(ModPlayer.PlayerConnect), m => m.PlayerConnect());
    }

    static void EnterWorld(On_Player_Hooks.orig_EnterWorld orig, int playerIndex)
    {
        orig(playerIndex);
        if (Main.netMode != 1 || playerIndex != Main.myPlayer) return;
        Player player = Main.player[playerIndex];
        if (!player.IsNull) PlayerLoader.Each(player, nameof(ModPlayer.SyncPlayer), playerIndex, static (m, i) => m.SyncPlayer(-1, i, true));
    }

    static void PlayerDisconnect(On_Player_Hooks.orig_PlayerDisconnect orig, int playerIndex)
    {
        Disconnect(playerIndex);
        orig(playerIndex);
    }

    static void SyncDisconnectedPlayer(On_NetMessage.orig_SyncDisconnectedPlayer orig, int plr)
    {
        Disconnect(plr);
        orig(plr);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Connected.Count == 0 || i != Main.myPlayer || Main.gameMenu || Main.netMode == 0) return;
        List<int>? gone = null;
        foreach ((int index, Entry entry) in Connected)
        {
            Player current = Main.player[index];
            if (current.IsNull || !current.active || current != entry.Player) (gone ??= new List<int>()).Add(index);
        }
        if (gone == null) return;
        foreach (int index in gone) Disconnect(index);
    }

    static void Connect(int index, Player player)
    {
        if (!Connected.TryGetValue(index, out Entry? previous) || previous.Player != player)
        {
            previous?.Pin.Dispose();
            Connected[index] = new Entry { Player = player, Pin = new GameHandle(player.Pointer) };
        }
        Disconnected.Remove(index);
    }

    static void Disconnect(int index)
    {
        Connected.Remove(index, out Entry? previous);
        if (previous == null && Disconnected.Contains(index)) return;
        Disconnected.Add(index);
        Player player = previous?.Player ?? ((uint)index < 255u ? Main.player[index] : default);
        try
        {
            if (!player.IsNull) PlayerLoader.Each(player, nameof(ModPlayer.PlayerDisconnect), m => m.PlayerDisconnect());
        }
        finally { previous?.Pin.Dispose(); }
    }
}
