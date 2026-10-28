using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;

namespace Terraria.ModLoader;

internal static unsafe class ItemNetHooks
{
    public const byte KindItem = 2;

    const int MaxBytes = 65000;

    static Hook? sendData, processData, reset;

    static bool globalWanted;

    sealed class Envelope
    {
        public byte Packet;
        public int Index;
        public int Slot;
        public string Key = "";
        public byte[]? Net;
        public string? Saved;
        public readonly List<(string Name, byte[] Data)> Globals = new();
        public long Time;
    }

    sealed class Receiving
    {
        public Envelope Envelope = null!;
        public int Target;
        public bool Applied;
    }

    static readonly object Gate = new();
    static readonly Dictionary<int, List<Envelope>> Pending = new();
    static readonly ConcurrentDictionary<(Type, string), bool> Overridden = new();
    static Receiving? receiving;

    static readonly List<int> Resend = new();

    static int inWorld;

    public static void Install(Func<string, bool> has, bool global)
    {
        bool net = has(nameof(ModItem.NetSend)) || has(nameof(ModItem.NetReceive));
        if (!net && (global || !has(nameof(ModItem.SaveData)))) return;
        if (global) globalWanted = true;
        ItemHooks.Once("NetItems", () =>
        {
            sendData = ItemHooks.Raw("Terraria", "NetMessage",
                "void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)",
                (IntPtr)(delegate* unmanaged<int, int, int, IntPtr, int, float, float, float, int, int, int, IntPtr, void>)&SendData);
            processData = ItemHooks.Raw("Terraria", "MessageBuffer", "void ProcessData(byte[] messageData, int length, out int messageType)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int, IntPtr, IntPtr, void>)&ProcessData);
            reset = ItemHooks.Raw("Terraria", "MessageBuffer", "void Reset(bool setupActive)",
                (IntPtr)(delegate* unmanaged<IntPtr, byte, IntPtr, void>)&Reset);
            On_Player.Update += (orig, self, i) =>
            {
                orig(self, i);
                if (i == Main.myPlayer) AfterJoin();
            };
        });
    }

    static bool IsItemPacket(int packet) => packet is 5 or 21 or 32 or 90;

    static bool Has(object instance, Type baseType, string name) =>
        Overridden.GetOrAdd((instance.GetType(), name), key => ItemHooks.Overrides(key.Item1, baseType, key.Item2));

    static string KeyOf(Item item) => ItemHooks.ModItemOf(item) is ModItem m ? m.FullName : "#" + item.type;

    static Item ItemFor(int packet, int index, int slot)
    {
        if (packet == 5)
        {
            if (index < 0 || index >= 255 || slot < 0 || slot > 1000) return default;
            Player player = Main.player[index];
            if (player.IsNull) return default;
            PlayerItemSlotID.SlotReference reference = PlayerItemSlotID.SlotReference.New(player, slot);
            return reference.Item;
        }
        if (packet == 32) return new Item(Host.Native()->ExpandChestItem(index, slot));
        var items = Main.item;
        if (index < 0 || index >= items.Length) return default;
        WorldItem w = items[index];
        return w.IsNull ? default : w.inner;
    }

    static void StoreChest(int chest, int slot, Item item)
    {
        if (Host.Native()->StoreChestItem(chest, slot, item.Pointer) != 0) InventoryStorage.SyncChestItemUpdateToStorage(chest, slot);
    }

    static byte[] Write(Action<BinaryWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream)) body(writer);
        return stream.ToArray();
    }

    static Envelope? Payload(Item item)
    {
        var envelope = new Envelope { Key = KeyOf(item) };
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            if (Has(m, typeof(ModItem), nameof(ModItem.NetSend)))
            {
                try { envelope.Net = Write(w => m.NetSend(w)); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.NetSend", e); }
            }
            else
            {
                try { envelope.Saved = ItemLoader.SavedText(m); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.SaveData", e); }
            }
        }
        if (globalWanted)
        {
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null || !Has(g, typeof(GlobalItem), nameof(GlobalItem.NetSend))) continue;
                try { envelope.Globals.Add((g.FullName, Write(w => g.NetSend(item, w)))); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.NetSend", e); }
            }
        }
        return envelope.Net == null && envelope.Saved == null && envelope.Globals.Count == 0 ? null : envelope;
    }

    static bool Apply(Item item, Envelope envelope)
    {
        if (item.IsNull || item.type <= 0 || KeyOf(item) != envelope.Key) return false;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try
            {
                if (envelope.Net != null)
                {
                    if (Has(m, typeof(ModItem), nameof(ModItem.NetReceive)))
                    {
                        using var reader = new BinaryReader(new MemoryStream(envelope.Net));
                        m.NetReceive(reader);
                    }
                }
                else if (envelope.Saved != null)
                {
                    ItemLoader.LoadText(m, envelope.Saved);
                }
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.NetReceive", e); }
        }
        if (envelope.Globals.Count > 0)
        {
            GlobalItem?[] globals = ItemHooks.GlobalsOf(item);
            foreach ((string name, byte[] data) in envelope.Globals)
            {
                GlobalItem? g = Array.Find(globals, x => x != null && x.FullName == name);
                if (g == null) continue;
                try
                {
                    using var reader = new BinaryReader(new MemoryStream(data));
                    g.NetReceive(item, reader);
                }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.NetReceive", e); }
            }
        }
        return true;
    }

    [UnmanagedCallersOnly]
    static void SendData(int msgType, int remote, int ignore, IntPtr text, int number, float number2, float number3, float number4,
                         int number5, int number6, int number7, IntPtr method)
    {
        Hook? hook = sendData;
        if (hook == null) return;
        if (IsItemPacket(msgType))
        {
            try { BeforeSend(msgType, remote, ignore, number, (int)number2); }
            catch (Exception e) { ItemHooks.Report("NetSend", e); }
        }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(msgType);
        call.Arg(remote);
        call.Arg(ignore);
        call.Arg(text);
        call.Arg(number);
        call.Arg(number2);
        call.Arg(number3);
        call.Arg(number4);
        call.Arg(number5);
        call.Arg(number6);
        call.Arg(number7);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "NetMessage.SendData");
    }

    static void AfterJoin()
    {
        if (ModNet.NetMode != 1 || Main.gameMenu)
        {
            inWorld = 0;
            return;
        }
        if (inWorld < 60 && ++inWorld < 60) return;
        int[] slots;
        lock (Gate)
        {
            if (Resend.Count == 0) return;
            slots = Resend.ToArray();
            Resend.Clear();
        }
        int me = Main.myPlayer;
        foreach (int slot in slots)
        {
            try
            {
                Item item = ItemFor(5, me, slot);
                if (item.IsNull || item.type <= 0 || item.stack <= 0) continue;
                NetMessage.SendData(5, -1, -1, default(Terraria.Localization.NetworkText), me, slot, item.prefix, 0, 0, 0, 0);
            }
            catch (Exception e) { ItemHooks.Report("NetSend (after joining)", e); }
        }
    }

    static void BeforeSend(int packet, int remote, int ignore, int index, int slot)
    {
        if (ModNet.NetMode == 0) return;
        if (packet != 5 && packet != 32) slot = 0;
        Item item = ItemFor(packet, index, slot);
        if (item.IsNull || item.type <= 0 || item.stack <= 0) return;
        if (ModNet.NetMode == 1 && (Main.gameMenu || inWorld < 60))
        {
            if (packet == 5 && index == Main.myPlayer && (ItemLoader.Get(item.type) != null || globalWanted))
                lock (Gate)
                    if (!Resend.Contains(slot)) Resend.Add(slot);
            return;
        }
        using var root = new GameHandle(item.Pointer);
        Receiving? incoming = receiving;
        if (incoming != null && !incoming.Applied && incoming.Envelope.Packet == packet &&
            (incoming.Envelope.Index == index || (packet == 21 && incoming.Envelope.Index == 400)) &&
            ((packet != 5 && packet != 32) || incoming.Envelope.Slot == slot))
        {
            incoming.Target = index;
            incoming.Applied = Apply(item, incoming.Envelope);
            if (incoming.Applied && packet == 32) StoreChest(index, slot, item);
        }
        if (ItemLoader.Get(item.type) == null && !globalWanted) return;
        Envelope? envelope = Payload(item);
        if (envelope == null) return;
        envelope.Packet = (byte)packet;
        envelope.Index = index;
        envelope.Slot = slot;
        byte[] bytes = Encode(envelope);
        if (bytes.Length > MaxBytes)
        {
            ItemHooks.Report($"{envelope.Key} network data", new InvalidOperationException($"{bytes.Length} bytes; the limit is {MaxBytes}"));
            return;
        }
        ModNet.Send(bytes, bytes.Length, remote, ignore);
    }

    static byte[] Encode(Envelope e) => Write(w =>
    {
        w.Write(KindItem);
        w.Write(e.Packet);
        w.Write(e.Index);
        w.Write(e.Slot);
        w.Write(e.Key);
        w.Write(e.Net != null);
        if (e.Net != null)
        {
            w.Write(e.Net.Length);
            w.Write(e.Net);
        }
        w.Write(e.Saved != null);
        if (e.Saved != null) w.Write(e.Saved);
        w.Write(e.Globals.Count);
        foreach ((string name, byte[] data) in e.Globals)
        {
            w.Write(name);
            w.Write(data.Length);
            w.Write(data);
        }
    });

    public static void Queue(BinaryReader reader, int from)
    {
        var e = new Envelope
        {
            Packet = reader.ReadByte(),
            Index = reader.ReadInt32(),
            Slot = reader.ReadInt32(),
            Key = reader.ReadString(),
        };
        if (reader.ReadBoolean()) e.Net = reader.ReadBytes(Math.Clamp(reader.ReadInt32(), 0, MaxBytes));
        if (reader.ReadBoolean()) e.Saved = reader.ReadString();
        int count = Math.Clamp(reader.ReadInt32(), 0, 256);
        for (int i = 0; i < count; i++) e.Globals.Add((reader.ReadString(), reader.ReadBytes(Math.Clamp(reader.ReadInt32(), 0, MaxBytes))));
        if (!IsItemPacket(e.Packet) || e.Index < 0 || e.Index >= 8000 || e.Slot < 0 || e.Slot > 65535 || e.Key.Length == 0) return;
        if ((e.Packet == 5 && e.Index > 255) || ((e.Packet == 21 || e.Packet == 90) && e.Index > 400)) return;
        if (ModNet.NetMode != 1 && e.Packet == 5 && e.Index != from) return;
        e.Time = Stopwatch.GetTimestamp();
        lock (Gate)
        {
            if (!Pending.TryGetValue(from, out List<Envelope>? queue)) Pending[from] = queue = new List<Envelope>();
            queue.RemoveAll(Expired);
            queue.Add(e);
            if (queue.Count > 64) queue.RemoveAt(0);
        }
    }

    static bool Expired(Envelope e) => Stopwatch.GetElapsedTime(e.Time).TotalSeconds > 10;

    static Envelope? Take(int from, int packet, int index, int slot)
    {
        lock (Gate)
        {
            if (!Pending.TryGetValue(from, out List<Envelope>? queue) || queue.Count == 0) return null;
            int at = queue.FindIndex(e => !Expired(e) && e.Packet == packet &&
                (e.Index == index || (packet == 21 && index == 400 && ModNet.NetMode != 1)) &&
                ((packet != 5 && packet != 32) || e.Slot == slot));
            if (at < 0) return null;
            Envelope found = queue[at];
            queue.RemoveAt(at);
            return found;
        }
    }

    static bool AnyPending()
    {
        lock (Gate)
        {
            foreach (List<Envelope> queue in Pending.Values)
                if (queue.Count > 0) return true;
            return false;
        }
    }

    [UnmanagedCallersOnly]
    static void ProcessData(IntPtr self, IntPtr data, int length, IntPtr typeOut, IntPtr method)
    {
        Hook? hook = processData;
        if (hook == null) return;
        Receiving? context = null;
        int packet = 0, index = 0, slot = 0;
        try
        {
            if (data != IntPtr.Zero && length >= 3 && length <= new GameArray<byte>(data).Length)
            {
                byte* b = (byte*)(data + 0x20);
                packet = b[0];
                if (IsItemPacket(packet) && !((packet == 5 || packet == 32) && length < 4) && AnyPending())
                {
                    int from = ModNet.NetMode == 1 ? 256 : new MessageBuffer(self).whoAmI;
                    index = packet == 5 ? b[1] : b[1] | (b[2] << 8);
                    slot = packet == 5 ? b[2] | (b[3] << 8) : packet == 32 ? b[3] : 0;
                    Envelope? envelope = Take(from, packet, index, slot);
                    if (envelope != null) context = new Receiving { Envelope = envelope, Target = index };
                }
            }
        }
        catch (Exception e) { ItemHooks.Report("NetReceive", e); }
        Receiving? outer = receiving;
        if (context != null) receiving = context;
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(self);
            call.Arg(data);
            call.Arg(length);
            call.Arg(typeOut);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "MessageBuffer.ProcessData");
        }
        finally { receiving = outer; }
        if (context == null || context.Applied) return;
        try
        {
            Item item = ItemFor(packet, context.Target, slot);
            if (item.IsNull) return;
            using var root = new GameHandle(item.Pointer);
            if (Apply(item, context.Envelope) && packet == 32) StoreChest(context.Target, slot, item);
        }
        catch (Exception e) { ItemHooks.Report("NetReceive", e); }
    }

    [UnmanagedCallersOnly]
    static void Reset(IntPtr self, byte setupActive, IntPtr method)
    {
        Hook? hook = reset;
        if (hook == null) return;
        try
        {
            int from = ModNet.NetMode == 1 ? 256 : new MessageBuffer(self).whoAmI;
            lock (Gate) Pending.Remove(from);
        }
        catch (Exception e) { ItemHooks.Report("NetReceive (reset)", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(setupActive);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "MessageBuffer.Reset");
    }
}
