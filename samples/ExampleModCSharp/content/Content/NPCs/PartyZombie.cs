using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs;

public class PartyZombie : ModNPC
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];

    public override void SetDefaults()
    {
        NPC.width = 18;
        NPC.height = 40;
        NPC.damage = 14;
        NPC.defense = 6;
        NPC.lifeMax = 200;
        NPC.value = 60f;
        NPC.knockBackResist = 0.5f;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath2;
        NPC.aiStyle = NPCAIStyleID.Fighter;
        AIType = NPCID.Zombie;
        AnimationType = NPCID.Zombie;
    }
}
