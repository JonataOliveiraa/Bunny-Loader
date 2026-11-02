using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class ExampleMountBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoTimeDisplay[Type] = true;
        Main.buffNoSave[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.mount.SetMount(ModContent.MountType<Mounts.ExampleMount>(), player);
        player.buffTime[buffIndex] = 10;
    }
}
