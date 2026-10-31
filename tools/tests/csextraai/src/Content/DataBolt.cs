using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsExtraAI.Content;

public sealed class DataBolt : ModProjectile
{
    public int Number;
    public string Text = "";
    public bool Dirty;

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = 0;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
    }

    public override void AI()
    {
        if (!Dirty) return;
        Dirty = false;
        Projectile.netUpdate = true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Number);
        writer.Write(Text);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Number = reader.ReadInt32();
        Text = reader.ReadString();
    }
}

public sealed class BitsGlobal : GlobalProjectile
{
    public bool First, Second;
    public float Value;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ModContent.ProjectileType<DataBolt>();

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        bitWriter.WriteBit(First);
        bitWriter.WriteBit(Second);
        binaryWriter.Write(Value);
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        First = bitReader.ReadBit();
        Second = bitReader.ReadBit();
        Value = binaryReader.ReadSingle();
    }
}
