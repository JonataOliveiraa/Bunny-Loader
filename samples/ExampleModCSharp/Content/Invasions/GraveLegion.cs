using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Invasions
{
    public class GraveLegion : ModInvasion
    {
        public override Color MessageColor => new Color(130, 230, 160);

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
                new InvasionEnemy(NPCID.DoctorBones, 0.3f, 5)),
        };

        public override int SpawnRate => 40;

        public override int Music => MusicID.PumpkinMoon;

        public override int MaxSpawns => 10;

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
