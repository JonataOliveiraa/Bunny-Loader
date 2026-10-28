using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Terraria.ModLoader.IO;

public class BigEndianWriter : BinaryWriter
{
    public BigEndianWriter(Stream output) : base(output, Encoding.UTF8, leaveOpen: true) { }

    public override void Write(short value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(ushort value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(int value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(uint value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(long value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(ulong value) => base.Write(BinaryPrimitives.ReverseEndianness(value));

    public override void Write(float value) => Write(System.BitConverter.SingleToInt32Bits(value));

    public override void Write(double value) => Write(System.BitConverter.DoubleToInt64Bits(value));
}
