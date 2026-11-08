using System;
using System.IO;
using BunnyLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader.Core;

internal static class AccessorySlotNet
{
    public const byte KindAccessorySlots = 21;

    static long lastSent;

    internal static void Install()
    {
        ItemNetHooks.WantSendData();
        ItemNetHooks.AfterSend += AfterSend;
    }

    static byte[] Packet(int index, ModAccessorySlotPlayer slots)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        int count = slots.LoadedSlotCount;
        writer.Write(KindAccessorySlots);
        writer.Write((byte)index);
        writer.Write((short)count);
        for (int i = 0; i < count; i++)
        {
            ItemIO.Send(slots.exAccessorySlot[i], writer, true);
            ItemIO.Send(slots.exAccessorySlot[i + slots.SlotCount], writer, true);
            ItemIO.Send(slots.exDyesAccessory[i], writer, true);
            writer.Write(slots.exHideAccessory[i]);
        }
        writer.Flush();
        return stream.ToArray();
    }

    static long Signature(ModAccessorySlotPlayer slots)
    {
        var hash = new HashCode();
        for (int i = 0; i < slots.LoadedSlotCount; i++)
        {
            foreach (Item item in new[] { slots.exAccessorySlot[i], slots.exAccessorySlot[i + slots.SlotCount], slots.exDyesAccessory[i] })
            {
                hash.Add(item.type);
                hash.Add(item.prefix);
                hash.Add(item.stack);
            }
            hash.Add(slots.exHideAccessory[i]);
        }
        return hash.ToHashCode() | 1L << 40;
    }

    static void Send(int index, int remote, int ignore)
    {
        Player player = Main.player[index];
        if (player.IsNull)
            return;
        ModAccessorySlotPlayer slots = AccessorySlotHooks.Of(player);
        if (slots.LoadedSlotCount == 0)
            return;
        byte[] packet = Packet(index, slots);
        if (!ModNet.Send(packet, packet.Length, remote, ignore))
            Log.Error("accessory slots: the message was not sent");
    }

    static void AfterSend(int msgType, int remote, int ignore, int number)
    {
        if (msgType != 4 || ModNet.NetMode == 0 || number < 0 || number > 254)
            return;
        try { Send(number, remote, ignore); }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot net", e); }
    }

    internal static void AfterUpdateEquips(Player player)
    {
        if (ModNet.NetMode == 0 || player.whoAmI != Main.myPlayer || Main.gameMenu)
            return;
        long signature = Signature(AccessorySlotHooks.Of(player));
        if (signature == lastSent)
            return;
        bool first = lastSent == 0;
        lastSent = signature;
        if (!first)
            Send(player.whoAmI, -1, -1);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        int index = reader.ReadByte();
        bool server = (ModNet.NetMode & 2) != 0;
        if (server && from >= 0)
            index = from;
        if (index < 0 || index > 254)
            return;
        Player player = Main.player[index];
        if (player.IsNull)
            return;
        ModAccessorySlotPlayer slots = AccessorySlotHooks.Of(player);
        int count = reader.ReadInt16();
        for (int i = 0; i < count; i++)
        {
            Item functional = Item.New(), vanity = Item.New(), dye = Item.New();
            ItemIO.Receive(functional, reader, true);
            ItemIO.Receive(vanity, reader, true);
            ItemIO.Receive(dye, reader, true);
            bool hidden = reader.ReadBoolean();
            if (i >= slots.LoadedSlotCount || index == Main.myPlayer)
                continue;
            slots.exAccessorySlot[i] = functional;
            slots.exAccessorySlot[i + slots.SlotCount] = vanity;
            slots.exDyesAccessory[i] = dye;
            slots.exHideAccessory[i] = hidden;
        }
        if (server)
            Send(index, -1, from);
    }
}
