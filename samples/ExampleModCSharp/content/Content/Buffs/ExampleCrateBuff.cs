using ExampleMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs;

public class ExampleCrateBuff : ModBuff
{
    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<ExampleFishingPlayer>().hasExampleCrateBuff = true;
    }
}
