using System;
using System.IO;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal const NPCMethod NetMethods = NPCMethod.SendExtraAI | NPCMethod.ReceiveExtraAI;

    public static void SendExtraAI(BinaryWriter writer, byte[] extraAI)
    {
        writer.Write7BitEncodedInt(extraAI.Length);
        if (extraAI.Length > 0) writer.Write(extraAI);
    }

    public static byte[]? WriteExtraAI(NPC npc)
    {
        using var stream = new MemoryStream();
        using var modWriter = new BinaryWriter(stream);
        using var bufferedStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(bufferedStream);
        var bitWriter = new BitWriter();
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.SendExtraAI))
        {
            try { own!.SendExtraAI(binaryWriter); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.SendExtraAI", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.SendExtraAI)) continue;
            try { global!.SendExtraAI(npc, bitWriter, binaryWriter); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.SendExtraAI", e); }
        }
        binaryWriter.Flush();
        bitWriter.Flush(modWriter);
        modWriter.Write(bufferedStream.ToArray());
        modWriter.Flush();
        byte[] bytes = stream.ToArray();
        return bytes.Length == 1 ? null : bytes;
    }

    public static byte[] ReadExtraAI(BinaryReader reader) => reader.ReadBytes(reader.Read7BitEncodedInt());

    public static void ReceiveExtraAI(NPC npc, byte[] extraAI)
    {
        using var stream = new MemoryStream(extraAI);
        using var modReader = new BinaryReader(stream);
        ModType? last = null;
        try
        {
            var bitReader = new BitReader(modReader);
            long bitReaderEnd = stream.Position;
            ModNPC? own = InstanceOf(npc);
            if (Has(own, NPCMethod.ReceiveExtraAI))
            {
                last = own;
                own!.ReceiveExtraAI(modReader);
            }
            foreach (GlobalNPC? global in Slots(npc))
            {
                if (!Has(global, NPCMethod.ReceiveExtraAI)) continue;
                last = global;
                global!.ReceiveExtraAI(npc, bitReader, modReader);
            }
            if (bitReader.BitsRead < bitReader.MaxBits)
                throw new IOException($"Read underflow {bitReader.MaxBits - bitReader.BitsRead} of {bitReader.MaxBits} compressed bits in ReceiveExtraAI");
            if (stream.Position < stream.Length)
                throw new IOException($"Read underflow {stream.Length - stream.Position} of {stream.Length - bitReaderEnd} bytes in ReceiveExtraAI");
        }
        catch (Exception e)
        {
            string owner = last?.FullName ?? InstanceOf(npc)?.FullName ?? $"NPC {npc.type}";
            ItemHooks.Report($"{owner}.ReceiveExtraAI", e);
        }
    }
}
