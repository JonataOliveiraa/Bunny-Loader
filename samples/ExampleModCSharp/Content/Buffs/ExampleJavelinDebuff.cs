using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{
    public class ExampleJavelinDebuff : ModBuff
    {
        public override void SetStaticDefaults() {

            BuffID.Sets.GrantImmunityWith[Type].Add(BuffID.BoneJavelin);
        }

        public override void Update(NPC npc, ref int buffIndex) {

        }
    }
}
