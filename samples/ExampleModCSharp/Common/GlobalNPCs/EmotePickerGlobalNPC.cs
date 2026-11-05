using ExampleMod.Common.Systems;
using ExampleMod.Content.Biomes;

using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    
    public class EmotePickerGlobalNPC : GlobalNPC
    {
        public override int? PickEmote(NPC npc, Player closestPlayer, List<int> emoteList, WorldUIAnchor otherAnchor) {

            if (Main.rand.NextBool(2) && ModContent.GetInstance<ExampleSurfaceBiome>().IsBiomeActive(closestPlayer)) {
                if (ModContent.TryFind<ModEmoteBubble>("ExampleMod/ExampleBiomeEmote", out var emote)) emoteList.Add(emote.Type);
            }

            if (Main.rand.NextBool(3) && DownedBossSystem.downedMinionBoss) {
                if (ModContent.TryFind<ModEmoteBubble>("ExampleMod/MinionBossEmote", out var emote)) emoteList.Add(emote.Type);
            }

            return base.PickEmote(npc, closestPlayer, emoteList, otherAnchor);
        }
    }
}
