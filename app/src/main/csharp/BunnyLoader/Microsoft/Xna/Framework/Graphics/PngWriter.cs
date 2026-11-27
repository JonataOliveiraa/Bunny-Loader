using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Microsoft.Xna.Framework.Graphics;

internal static class PngWriter
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    static readonly uint[] CrcTable = BuildCrcTable();

    internal static void Write(Stream stream, Texture2D texture, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "SaveAsPng: the size must be positive");

        int sourceWidth = texture.Width, sourceHeight = texture.Height;
        var pixels = new Color[sourceWidth * sourceHeight];
        texture.GetData(pixels);

        int stride = width * 4 + 1;
        byte[] raw = new byte[stride * height];

        bool sameSize = width == sourceWidth && height == sourceHeight;

        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            int sourceRow = (sameSize ? y : (int)((long)y * sourceHeight / height)) * sourceWidth;

            for (int x = 0; x < width; x++)
            {
                Color c = pixels[sourceRow + (sameSize ? x : (int)((long)x * sourceWidth / width))];
                int at = row + 1 + x * 4;
                raw[at] = c.R;
                raw[at + 1] = c.G;
                raw[at + 2] = c.B;
                raw[at + 3] = c.A;
            }
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;

        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
            zlib.Write(raw, 0, raw.Length);

        stream.Write(Signature, 0, Signature.Length);
        WriteChunk(stream, "IHDR", header);
        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        byte[] name = Encoding.ASCII.GetBytes(type);

        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        stream.Write(name, 0, name.Length);
        stream.Write(data, 0, data.Length);

        uint crc = Crc(0xFFFFFFFFu, name);
        crc = Crc(crc, data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        stream.Write(number);
    }

    static uint Crc(uint crc, byte[] data)
    {
        foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
