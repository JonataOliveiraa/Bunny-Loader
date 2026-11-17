using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Invasions
{
    // ModInvasion lets mods add invasions like the Goblin Army or the Pirate Invasion, with waves, a progress bar and an icon.
    // Grave Legion is a three wave undead invasion, started by using a GraveBell. See PaladinCrusade for a smaller one.
    // Its name comes from the Invasions entries of the localization files, where PaladinCrusade also has custom Start, Fail and Complete messages.
    // The progress bar icon is GraveLegion_Icon.png, next to this file.
    public class GraveLegion : ModInvasion
    {
        // The color of the chat messages about this invasion.
        public override Color MessageColor => new Color(130, 230, 160);

        // Each wave needs a number of points to be cleared, earned by killing its enemies.
        // InvasionEnemy takes the NPC type, its spawn weight (how likely it is to be picked compared to the others in the wave, 1 by default)
        // and how many points a kill is worth (1 by default). The points needed grow with the number of players, see ModInvasion.ScalePoints.
        public override IList<InvasionWave> Waves { get; } = new[] {
            new InvasionWave(15,
                new InvasionEnemy(NPCID.Zombie, 3),
                new InvasionEnemy(NPCID.Skeleton, 2)),
            new InvasionWave(20,
                new InvasionEnemy(NPCID.Skeleton, 2),
                new InvasionEnemy(NPCID.AngryBones, 2),
                new InvasionEnemy(NPCID.UndeadViking, 1, 2),
                new InvasionEnemy(NPCID.CursedSkull)),
            new InvasionWave(25,
                new InvasionEnemy(NPCID.AngryBones, 2),
                new InvasionEnemy(NPCID.DarkCaster, 1, 2),
                new InvasionEnemy(NPCID.Ghost, 1, 2),
                new InvasionEnemy(NPCID.DoctorBones, 0.3f, 5)), // Doctor Bones is rare, but a kill is worth 5 points.
        };

        // These replace the normal spawn rate and enemy cap while the invasion is active, like in GlobalNPC.EditSpawnRate. A lower SpawnRate means more frequent spawns.
        public override int SpawnRate => 40;

        // The music that plays while the invasion is active. Use MusicLoader.GetMusicSlot for modded music.
        public override int Music => MusicID.PumpkinMoon;

        public override int MaxSpawns => 10;

        // OnComplete runs once the last wave is cleared. Here every living player gets 5 gold coins as a reward.
        public override void OnComplete() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.active && !player.dead) {
                    Item.NewItem(player.GetSource_FromThis(), player.Center, ItemID.GoldCoin, 5);
                }
            }
        }
    }
}
