using ExampleMod.Content.NPCs;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

public class PartyZombieKilled : ModAchievement
{
    public override void SetStaticDefaults()
    {
        AddNPCKilledCondition(ModContent.NPCType<PartyZombie>());
    }

    public override Position GetDefaultPosition() => new After("EYE_ON_YOU");

    public override Position GetAdvisorPosition() => new After("EYE_ON_YOU");
}
