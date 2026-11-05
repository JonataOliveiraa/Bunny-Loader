using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{

    public class BuffImmuneGlobalNPC : GlobalNPC
    {
        public override void SetStaticDefaults() {

            SetDefenseDebuffStaticDefaults(BuffID.BetsysCurse);

            NPCID.Sets.SpecificDebuffImmunity[NPCID.DesertGhoulCrimson][BuffID.Ichor] = true;

            NPCID.Sets.SpecificDebuffImmunity[NPCID.Paladin][BuffID.Ichor] = false;
        }

        public static void SetDefenseDebuffStaticDefaults(int buffType) {
            
            BuffID.Sets.GrantImmunityWith[buffType].Add(BuffID.Ichor);
        }

        public override void SetDefaults(NPC entity) {

        }
    }
}
