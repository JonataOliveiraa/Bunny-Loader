using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Terraria.ModLoader.IO;

public static class TagIO
{
    const byte IdEnd = 0, IdByte = 1, IdShort = 2, IdInt = 3, IdLong = 4, IdFloat = 5, IdDouble = 6, IdByteArray = 7, IdString = 8, IdList = 9, IdCompound = 10, IdIntArray = 11;

    public static void ToFile(TagCompound root, string path, bool compress = true)
    {
        using FileStream stream = File.Create(path);
        ToStream(root, stream, compress);
    }

    public static TagCompound FromFile(string path, bool compressed = true)
    {
        using FileStream stream = File.OpenRead(path);
        return FromStream(stream, compressed);
    }

    public static void ToStream(TagCompound root, Stream stream, bool compress = true)
    {
        if (compress)
        {
            using var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            Write(root, new BigEndianWriter(gzip));
        }
        else
        {
            Write(root, new BigEndianWriter(stream));
        }
    }

    public static TagCompound FromStream(Stream stream, bool compressed = true)
    {
        if (!compressed) return Read(new BigEndianReader(stream));
        using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        return Read(new BigEndianReader(gzip));
    }

    public static void Write(TagCompound root, BinaryWriter writer)
    {
        writer.Write(IdCompound);
        WriteString(writer, "");
        WritePayload(writer, root);
        writer.Flush();
    }

    public static TagCompound Read(BinaryReader reader)
    {
        byte id = reader.ReadByte();
        if (id != IdCompound) throw new IOException($"TagIO: the root is tag {id}, not a compound");
        ReadString(reader);
        return (TagCompound)ReadPayload(reader, IdCompound, 0);
    }

    static byte IdOf(object value) => value switch
    {
        byte => IdByte,
        short => IdShort,
        int => IdInt,
        long => IdLong,
        float => IdFloat,
        double => IdDouble,
        byte[] => IdByteArray,
        string => IdString,
        List<object> => IdList,
        TagCompound => IdCompound,
        int[] => IdIntArray,
        _ => throw new IOException($"TagIO: {value.GetType().Name} cannot be written"),
    };

    static void WritePayload(BinaryWriter writer, object value)
    {
        switch (value)
        {
            case byte b: writer.Write(b); break;
            case short s: writer.Write(s); break;
            case int i: writer.Write(i); break;
            case long l: writer.Write(l); break;
            case float f: writer.Write(f); break;
            case double d: writer.Write(d); break;
            case byte[] bytes:
                writer.Write(bytes.Length);
                writer.Write(bytes);
                break;
            case string s: WriteString(writer, s); break;
            case List<object> list:
                byte element = list.Count == 0 ? IdEnd : IdOf(list[0]);
                foreach (object item in list)
                {
                    if (IdOf(item) != element) throw new IOException("TagIO: a list must hold one kind of value");
                }
                writer.Write(element);
                writer.Write(list.Count);
                foreach (object item in list) WritePayload(writer, item);
                break;
            case TagCompound tag:
                foreach ((string key, object entry) in tag)
                {
                    writer.Write(IdOf(entry));
                    WriteString(writer, key);
                    WritePayload(writer, entry);
                }
                writer.Write(IdEnd);
                break;
            case int[] ints:
                writer.Write(ints.Length);
                foreach (int i in ints) writer.Write(i);
                break;
            default:
                throw new IOException($"TagIO: {value.GetType().Name} cannot be written");
        }
    }

    static object ReadPayload(BinaryReader reader, byte id, int depth)
    {
        if (depth > 512) throw new IOException("TagIO: too deep");
        switch (id)
        {
            case IdByte: return reader.ReadByte();
            case IdShort: return reader.ReadInt16();
            case IdInt: return reader.ReadInt32();
            case IdLong: return reader.ReadInt64();
            case IdFloat: return reader.ReadSingle();
            case IdDouble: return reader.ReadDouble();
            case IdByteArray: return reader.ReadBytes(Length(reader));
            case IdString: return ReadString(reader);
            case IdList:
                byte element = reader.ReadByte();
                int count = Length(reader);
                var list = new List<object>(count);
                for (int i = 0; i < count; i++) list.Add(ReadPayload(reader, element, depth + 1));
                return list;
            case IdCompound:
                var tag = new TagCompound();
                while (true)
                {
                    byte entry = reader.ReadByte();
                    if (entry == IdEnd) return tag;
                    string key = ReadString(reader);
                    tag.Set(key, ReadPayload(reader, entry, depth + 1), true);
                }
            case IdIntArray:
                int length = Length(reader);
                var ints = new int[length];
                for (int i = 0; i < length; i++) ints[i] = reader.ReadInt32();
                return ints;
        }
        throw new IOException($"TagIO: unknown tag {id}");
    }

    static int Length(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0) throw new IOException("TagIO: negative length");
        return length;
    }

    static void WriteString(BinaryWriter writer, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > ushort.MaxValue) throw new IOException("TagIO: string too long");
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    static string ReadString(BinaryReader reader) => Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
}
