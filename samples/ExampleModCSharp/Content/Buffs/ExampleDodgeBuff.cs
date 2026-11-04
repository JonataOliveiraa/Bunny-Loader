using ExampleMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{

    internal class ExampleDodgeBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {

        }
    }
}
