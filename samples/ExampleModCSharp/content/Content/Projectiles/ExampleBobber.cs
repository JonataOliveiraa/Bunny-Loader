using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleBobber : ModProjectile
{
    public static readonly Color[] PossibleLineColors =
    [
        new Color(255, 215, 0),
        new Color(0, 191, 255),
    ];

    private int fishingLineColorIndex;

    public Color FishingLineColor => PossibleLineColors[fishingLineColorIndex];

    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.BobberWooden);

        DrawOriginOffsetY = -8;
    }

    public override void OnSpawn(IEntitySource source)
    {
        fishingLineColorIndex = (byte)Main.rand.Next(PossibleLineColors.Length);
    }

    public override void AI()
    {
        if (!Main.dedServ)
        {
            Lighting.AddLight(Projectile.Center, FishingLineColor.ToVector3());
        }
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((byte)fishingLineColorIndex);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        fishingLineColorIndex = reader.ReadByte();
    }
}
