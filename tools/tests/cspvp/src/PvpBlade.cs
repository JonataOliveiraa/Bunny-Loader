using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CsPvp;

public class PvpBlade : ModItem
{
    public static bool Veto;
    public static int CanHitCalls, ModifyCalls, OnHitCalls, LastMark = -1;
    public static Player.HurtInfo LastInfo;

    public int Mark;

    protected override bool CloneNewInstances => true;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.damage = 20;
        Item.melee = true;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = 1;
    }

    public override void NetSend(BinaryWriter writer) => writer.Write(Mark);

    public override void NetReceive(BinaryReader reader) => Mark = reader.ReadInt32();

    public override bool CanHitPvp(Player player, Player target)
    {
        CanHitCalls++;
        return !Veto;
    }

    public override void ModifyHitPvp(Player player, Player target, ref Player.HurtModifiers modifiers)
    {
        ModifyCalls++;
        modifiers.SourceDamage *= 2f;
    }

    public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo)
    {
        OnHitCalls++;
        LastMark = Mark;
        LastInfo = hurtInfo;
    }
}
