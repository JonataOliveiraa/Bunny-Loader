using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{

    public class ExampleWhipAdvancedPlayerBuff : ModBuff
    {
        public override void Update(Player player, ref int buffIndex) {

            player.GetAttackSpeed(DamageClass.SummonMeleeSpeed) += 0.12f;
        }
    }
}
