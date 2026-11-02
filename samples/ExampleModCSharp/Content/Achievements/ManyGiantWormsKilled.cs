using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

public class ManyGiantWormsKilled : ModAchievement
{
    public CustomIntCondition Condition;

    public override bool Hidden => Condition.Value == 0;

    public override void SetStaticDefaults()
    {
        Achievement.SetCategory(AchievementCategory.Collector);
        Condition = AddIntCondition(5);
        AchievementsHelper.OnNPCKilled += NPCKilledListener;
    }

    public override void Unload()
    {
        AchievementsHelper.OnNPCKilled -= NPCKilledListener;
    }

    private void NPCKilledListener(Player player, short npcId)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        if (npcId == NPCID.GiantWormHead)
            Condition.Value++;
    }
}
