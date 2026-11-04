using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{

    public class ExampleWeaponImbue : ModBuff
    {
        public override void SetStaticDefaults() {
            BuffID.Sets.IsAFlaskBuff[Type] = true;
            Main.meleeBuff[Type] = true;
            Main.persistentBuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {

            player.MeleeEnchantActive = true;
        }
    }
}
