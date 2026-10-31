using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class ItemPvpHooks
{
    public const byte KindPvp = 3;

    public const int HurtPacket = 117;

    internal sealed class Received
    {
        public int Attacker, Target, Type, Prefix;
        public ItemNetHooks.Envelope? Item;
        public long Time;
    }

    sealed class Attack
    {
        public Player Player;
        public Item Item;
    }

    static readonly object Gate = new();
    static readonly Dictionary<int, List<Received>> Pending = new();
    static Attack? local;
    static bool globalPvp;

    internal static Received? Receiving;

    internal static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.CanHitPvp)))
        {
            if (global) globalPvp = true;
            WantCanHit();
        }
        if (has(nameof(ModItem.ModifyHitPvp)) || has(nameof(ModItem.OnHitPvp)))
        {
            if (global) globalPvp = true;
            PlayerHurtHooks.Want();
            WantNet();
        }
    }

    internal static void WantCanHit() => ItemHooks.Once("PvpCanHit", () =>
    {
        On_Player.ItemCheck_MeleeHitPVP += MeleeHitPvp;
        On_Player.CanHit += CanHit;
    });

    static void WantNet() => ItemHooks.Once("PvpNet", () =>
    {
        ItemNetHooks.WantProcessData();
        On_NetMessage.SendPlayerHurt += SendPlayerHurt;
    });

    static void MeleeHitPvp(On_Player.orig_ItemCheck_MeleeHitPVP orig, Player self, Item sItem, Microsoft.Xna.Framework.Rectangle itemRectangle, int damage,
                            float knockBack)
    {
        Attack? outer = local;
        local = new Attack { Player = self, Item = sItem };
        try { orig(self, sItem, itemRectangle, damage, knockBack); }
        finally { local = outer; }
    }

    static bool CanHit(On_Player.orig_CanHit orig, Player self, Entity ent)
    {
        bool result = orig(self, ent);
        Attack? attack = local;
        if (!result || attack == null || attack.Player != self || ent.IsNull || !GameInterop.IsInstance(ent.Pointer, Player.Class)) return result;
        try { return CanHitPvp(attack.Item, self, new Player(ent.Pointer)); }
        catch (Exception e)
        {
            ItemHooks.Report("CanHitPvp", e);
            return result;
        }
    }

    static bool CanHitPvp(Item item, Player player, Player target)
    {
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null || !ItemHooks.Wants(g, nameof(GlobalItem.CanHitPvp))) continue;
            try
            {
                if (!g.CanHitPvp(item, player, target)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanHitPvp", e); }
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try
            {
                if (!m.CanHitPvp(player, target)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanHitPvp", e); }
        }
        return PlayerLoader.All(player, nameof(ModPlayer.CanHitPvp), (item, target), static (pm, s) => pm.CanHitPvp(s.item, s.target));
    }

    internal static void ModifyHitPvp(Item item, Player player, Player target, ref Player.HurtModifiers modifiers)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyHitPvp(player, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyHitPvp", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyHitPvp(item, player, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyHitPvp", e); }
        }
    }

    internal static void OnHitPvp(Item item, Player player, Player target, Player.HurtInfo info)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.OnHitPvp(player, target, info); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.OnHitPvp", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.OnHitPvp(item, player, target, info); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnHitPvp", e); }
        }
    }

    internal static bool Source(PlayerDeathReason src, int target, out Player player, out Item item, out GameHandle? pin)
    {
        player = default;
        item = default;
        pin = null;
        if (src.IsNull || src._sourceNPCIndex >= 0 || src._sourceProjectileLocalIndex >= 0 || src._sourceProjectileType > 0) return false;
        int index = src._sourcePlayerIndex, type = src._sourceItemType, prefix = src._sourceItemPrefix;
        if ((uint)index >= 255u || type <= 0 || (type >= Terraria.ID.ItemID.Count && ItemLoader.Get(type) == null)) return false;
        player = Main.player[index];
        if (player.IsNull || !player.active) return false;
        Received? received = Receiving;
        if (received != null && received.Target == target && received.Attacker == index && received.Type == type && received.Prefix == prefix)
        {
            item = Fresh(type, prefix, out pin);
            if (received.Item != null) ItemNetHooks.Apply(item, received.Item);
            return true;
        }
        Attack? attack = local;
        if (attack != null && attack.Player == player && attack.Item.type == type)
        {
            item = attack.Item;
            return true;
        }
        int slot = player.selectedItem;
        Item held = (uint)slot < 59u ? player.inventory[slot] : default;
        if (!held.IsNull && held.type == type && held.prefix == prefix)
        {
            item = held;
            return true;
        }
        item = Fresh(type, prefix, out pin);
        return true;
    }

    static Item Fresh(int type, int prefix, out GameHandle? pin)
    {
        Item item = Item.New();
        pin = new GameHandle(item.Pointer);
        item.SetDefaults(type, default);
        if (prefix > 0) item.Prefix(prefix);
        return item;
    }

    static void SendPlayerHurt(On_NetMessage.orig_SendPlayerHurt orig, int playerTargetIndex, PlayerDeathReason reason, int damage, int direction, bool critical,
                               bool pvp, int hitContext, int remoteClient, int ignoreClient)
    {
        if (pvp && ModNet.NetMode != 0)
        {
            try { Announce(playerTargetIndex, reason, remoteClient, ignoreClient); }
            catch (Exception e) { ItemHooks.Report("PvP item (send)", e); }
        }
        orig(playerTargetIndex, reason, damage, direction, critical, pvp, hitContext, remoteClient, ignoreClient);
    }

    static void Announce(int target, PlayerDeathReason reason, int remote, int ignore)
    {
        Received? received = Receiving;
        if (received != null && received.Target == target && !reason.IsNull && received.Attacker == reason._sourcePlayerIndex)
        {
            Send(received.Attacker, target, received.Type, received.Prefix, received.Item, remote, ignore);
            return;
        }
        if (!Source(reason, target, out Player player, out Item item, out GameHandle? pin)) return;
        using (pin)
        {
            if (ItemLoader.Get(item.type) == null && !globalPvp) return;
            Send(player.whoAmI, target, item.type, item.prefix, ItemNetHooks.Payload(item), remote, ignore);
        }
    }

    static void Send(int attacker, int target, int type, int prefix, ItemNetHooks.Envelope? payload, int remote, int ignore)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream))
        {
            w.Write(KindPvp);
            w.Write((byte)attacker);
            w.Write((byte)target);
            w.Write(type);
            w.Write(prefix);
            w.Write(payload != null);
            if (payload != null) ItemNetHooks.WriteBody(w, payload);
        }
        byte[] bytes = stream.ToArray();
        ModNet.Send(bytes, bytes.Length, remote, ignore);
    }

    public static void Queue(BinaryReader reader, int from)
    {
        var r = new Received
        {
            Attacker = reader.ReadByte(),
            Target = reader.ReadByte(),
            Type = reader.ReadInt32(),
            Prefix = reader.ReadInt32(),
        };
        if (reader.ReadBoolean()) r.Item = ItemNetHooks.ReadBody(reader);
        if (r.Attacker >= 255 || r.Target >= 255 || r.Type <= 0 || r.Prefix < 0 || r.Prefix > 255) return;
        if (r.Type >= Terraria.ID.ItemID.Count && ItemLoader.Get(r.Type) == null) return;
        if (ModNet.NetMode != 1 && from != r.Attacker) return;
        r.Time = Stopwatch.GetTimestamp();
        lock (Gate)
        {
            if (!Pending.TryGetValue(from, out List<Received>? queue)) Pending[from] = queue = new List<Received>();
            queue.RemoveAll(Expired);
            if (queue.Count >= 16) queue.RemoveAt(0);
            queue.Add(r);
        }
    }

    static bool Expired(Received r) => Stopwatch.GetElapsedTime(r.Time).TotalSeconds > 5;

    internal static Received? Take(int from, int target)
    {
        lock (Gate)
        {
            if (Pending.Count == 0 || !Pending.TryGetValue(from, out List<Received>? queue)) return null;
            queue.RemoveAll(Expired);
            Received? found = null;
            if (queue.Count > 0 && queue[0].Target == target)
            {
                found = queue[0];
                queue.RemoveAt(0);
            }
            if (queue.Count == 0) Pending.Remove(from);
            return found;
        }
    }

    internal static void Forget(int from)
    {
        lock (Gate) Pending.Remove(from);
    }
}
