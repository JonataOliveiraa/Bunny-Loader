using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Terraria.ModLoader.IO;

public class BigEndianReader : BinaryReader
{
    public BigEndianReader(Stream input) : base(input, Encoding.UTF8, leaveOpen: true) { }

    public override short ReadInt16() => BinaryPrimitives.ReverseEndianness(base.ReadInt16());

    public override ushort ReadUInt16() => BinaryPrimitives.ReverseEndianness(base.ReadUInt16());

    public override int ReadInt32() => BinaryPrimitives.ReverseEndianness(base.ReadInt32());

    public override uint ReadUInt32() => BinaryPrimitives.ReverseEndianness(base.ReadUInt32());

    public override long ReadInt64() => BinaryPrimitives.ReverseEndianness(base.ReadInt64());

    public override ulong ReadUInt64() => BinaryPrimitives.ReverseEndianness(base.ReadUInt64());

    public override float ReadSingle() => System.BitConverter.Int32BitsToSingle(ReadInt32());

    public override double ReadDouble() => System.BitConverter.Int64BitsToDouble(ReadInt64());
}
