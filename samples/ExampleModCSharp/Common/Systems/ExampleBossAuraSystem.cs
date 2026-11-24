using BunnyLoader;
using ExampleMod.Content.Effects;
using ExampleMod.Content.NPCs.MinionBoss;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

// Spawns ExampleBossAuraEffect for every Minion Boss alive. On PC, a tModLoader mod would activate a Filter from its boss instead.
public class ExampleBossAuraSystem : ModSystem
{
    public override void PostUpdateNPCs()
    {
        // Screen effects only exist where the game is drawn.
        if (Main.dedServ)
            return;

        int type = ModContent.NPCType<MinionBossBody>();
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.type != type)
                continue;

            // The key makes sure each boss has a single aura. Spawning again with the same key would restart its fade in.
            string key = "ExampleMod:BossAura" + npc.whoAmI;
            if (ScreenEffects.Find(key) != null)
                continue;

            // Spawning on an NPC makes the aura follow it and end when it dies.
            ScreenEffects.Spawn<ExampleBossAuraEffect>(npc, fx =>
            {
                fx.Key = key;
                fx.Boss = npc.whoAmI;
            });
        }
    }
}
