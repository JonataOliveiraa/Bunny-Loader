using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

// This achievement shows a counter condition fed by a game event: it completes after the player kills 5 Giant Worms.
// See MinionBossKilled and AdvancedExampleAchievement for the other kinds of conditions.
public class ManyGiantWormsKilled : ModAchievement
{
    public CustomIntCondition Condition;

    // Hidden achievements show "???" instead of their name and description until completed. This one stays hidden until the first Giant Worm is killed.
    public override bool Hidden => Condition.Value == 0;

    public override void SetStaticDefaults()
    {
        Achievement.SetCategory(AchievementCategory.Collector);
        // AddIntCondition creates a condition that completes once its Value reaches the given number. The progress bar shows Value out of 5.
        Condition = AddIntCondition(5);
        // AchievementsHelper.OnNPCKilled is the event vanilla achievements use to count kills.
        AchievementsHelper.OnNPCKilled += NPCKilledListener;
    }

    // Events must be unsubscribed when the mod unloads, otherwise the game keeps calling into an unloaded mod.
    public override void Unload()
    {
        AchievementsHelper.OnNPCKilled -= NPCKilledListener;
    }

    private void NPCKilledListener(Player player, short npcId)
    {
        // Achievements belong to the local player only.
        if (player.whoAmI != Main.myPlayer)
            return;

        // Worms are made of several NPCs, so only the head is counted.
        if (npcId == NPCID.GiantWormHead)
            Condition.Value++;
    }
}
