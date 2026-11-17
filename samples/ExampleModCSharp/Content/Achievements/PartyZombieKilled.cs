using ExampleMod.Content.NPCs;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

// The simplest kind of achievement: it completes the first time the player kills a PartyZombie.
public class PartyZombieKilled : ModAchievement
{
    public override void SetStaticDefaults()
    {
        AddNPCKilledCondition(ModContent.NPCType<PartyZombie>());
    }

    // Places this achievement right after the vanilla "Eye on You" achievement in the achievements menu.
    public override Position GetDefaultPosition() => new After("EYE_ON_YOU");

    // The advisor is the card that suggests the next achievement to work towards. Without this, the achievement is never suggested.
    public override Position GetAdvisorPosition() => new After("EYE_ON_YOU");
}
