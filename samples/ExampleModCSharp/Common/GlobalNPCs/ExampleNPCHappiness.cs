using ExampleMod.Content.Biomes;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    public class ExampleNPCHappiness : GlobalNPC
    {
        public override void SetStaticDefaults() {
            int examplePersonType = ModContent.NPCType<Content.NPCs.ExamplePerson>(); 
            var guideHappiness = NPCHappiness.Get(NPCID.Guide); 
            var zoologistHappiness = NPCHappiness.Get(NPCID.BestiaryGirl); 

            guideHappiness.SetNPCAffection(examplePersonType, AffectionLevel.Love); 
            zoologistHappiness.SetNPCAffection(examplePersonType, AffectionLevel.Like); 

            guideHappiness.SetBiomeAffection<ExampleSurfaceBiome>(AffectionLevel.Love);  
        }
    }
}
