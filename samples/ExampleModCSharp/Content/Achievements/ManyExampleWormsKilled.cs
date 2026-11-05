using ExampleMod.Content.NPCs;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

public class ManyExampleWormsKilled : ModAchievement
{
    public CustomIntCondition Condition ;

    public override bool Hidden => Condition.Value == 0;

    public override void SetStaticDefaults() {
        Condition = AddIntCondition(5);

        AchievementsHelper.OnNPCKilled += NPCKilledListener;
    }

    public override void Unload() {
        AchievementsHelper.OnNPCKilled -= NPCKilledListener;
    }

    private void NPCKilledListener(Player player, short npcId) {
        if (player.whoAmI != Main.myPlayer) {
            return;
        }

        if (npcId == ModContent.NPCType<ExampleWormHead>()) {
            Condition.Value++;
        }
    }
}
