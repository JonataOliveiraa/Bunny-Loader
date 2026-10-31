using Terraria.ModLoader.Utilities;
using Terraria;
using Terraria.ID;
using Terraria.GameContent.ItemDropRules;
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

    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (modifiers.DamageType.CountsAsClass(DamageClass.Magic)) modifiers.FinalDamage *= 0.25f;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(NPCID.Zombie, false)) npcLoot.Add(rule);
        npcLoot.Add(ItemDropRule.Common(ItemID.Confetti, 100));
    }

    public override float SpawnChance(NPC.Spawner spawner) => SpawnCondition.OverworldNightMonster.Chance * 0.2f;
}
