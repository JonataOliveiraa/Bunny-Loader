using System;
using System.IO;

namespace Terraria.ModLoader.IO;

public static class BinaryIO
{
    public static void ReadFlags(this BinaryReader reader, out bool b0)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2, out bool b3)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
        b3 = (bits & 8) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2, out bool b3, out bool b4)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
        b3 = (bits & 8) != 0;
        b4 = (bits & 16) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2, out bool b3, out bool b4, out bool b5)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
        b3 = (bits & 8) != 0;
        b4 = (bits & 16) != 0;
        b5 = (bits & 32) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2, out bool b3, out bool b4, out bool b5, out bool b6)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
        b3 = (bits & 8) != 0;
        b4 = (bits & 16) != 0;
        b5 = (bits & 32) != 0;
        b6 = (bits & 64) != 0;
    }

    public static void ReadFlags(this BinaryReader reader, out bool b0, out bool b1, out bool b2, out bool b3, out bool b4, out bool b5, out bool b6, out bool b7)
    {
        byte bits = reader.ReadByte();
        b0 = (bits & 1) != 0;
        b1 = (bits & 2) != 0;
        b2 = (bits & 4) != 0;
        b3 = (bits & 8) != 0;
        b4 = (bits & 16) != 0;
        b5 = (bits & 32) != 0;
        b6 = (bits & 64) != 0;
        b7 = (bits & 128) != 0;
    }

    public static void WriteFlags(this BinaryWriter writer, bool b1 = false, bool b2 = false, bool b3 = false, bool b4 = false, bool b5 = false, bool b6 = false, bool b7 = false, bool b8 = false) =>
        writer.Write(Pack(b1, b2, b3, b4, b5, b6, b7, b8));

    public static void Write(this BinaryWriter writer, bool b1 = false, bool b2 = false, bool b3 = false, bool b4 = false, bool b5 = false, bool b6 = false, bool b7 = false, bool b8 = false) =>
        writer.Write(Pack(b1, b2, b3, b4, b5, b6, b7, b8));

    public static void SafeWrite(this BinaryWriter writer, Action<BinaryWriter> write)
    {
        var stream = new MemoryStream();
        write(new BinaryWriter(stream));
        writer.Write7BitEncodedInt((int)stream.Length);
        stream.Position = 0;
        stream.CopyTo(writer.BaseStream);
    }

    public static void SafeRead(this BinaryReader reader, Action<BinaryReader> read)
    {
        int length = reader.Read7BitEncodedInt();
        var stream = reader.ReadBytes(length).ToMemoryStream();
        read(new BinaryReader(stream));
        if (stream.Position != length) throw new IOException("Read underflow " + stream.Position + " of " + length + " bytes");
    }

    public static void ReadBytes(this Stream stream, byte[] buf)
    {
        int read, position = 0;
        while ((read = stream.Read(buf, position, buf.Length - position)) > 0) position += read;
        if (position != buf.Length) throw new IOException($"Stream did not contain enough bytes ({position}) < ({buf.Length})");
    }

    public static byte[] ReadBytes(this Stream stream, int len) => ReadBytes(stream, (long)len);

    public static byte[] ReadBytes(this Stream stream, long len)
    {
        var buf = new byte[len];
        stream.ReadBytes(buf);
        return buf;
    }

    public static MemoryStream ToMemoryStream(this byte[] bytes, bool writeable = false) =>
        new(bytes, 0, bytes.Length, writeable, publiclyVisible: true);

    static byte Pack(bool b1, bool b2, bool b3, bool b4, bool b5, bool b6, bool b7, bool b8) =>
        (byte)((b1 ? 1 : 0) | (b2 ? 2 : 0) | (b3 ? 4 : 0) | (b4 ? 8 : 0) | (b5 ? 16 : 0) | (b6 ? 32 : 0) | (b7 ? 64 : 0) | (b8 ? 128 : 0));
}
