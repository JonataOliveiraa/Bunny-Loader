using ExampleMod.Content.NPCs.MinionBoss;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

public class MinionBossKilled : ModAchievement
{
    public override void SetStaticDefaults() {
        AddNPCKilledCondition(ModContent.NPCType<MinionBossBody>());
    }

    public override Position GetDefaultPosition() => new After("EYE_ON_YOU");

    public override Position GetAdvisorPosition() => new After("EYE_ON_YOU");
}
