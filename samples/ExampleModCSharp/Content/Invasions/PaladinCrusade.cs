using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Invasions
{
    public class PaladinCrusade : ModInvasion
    {
        public override Color MessageColor => new Color(255, 215, 90);

        public override IList<InvasionWave> Waves { get; } = new[] { new InvasionWave(12, new InvasionEnemy(NPCID.Paladin)) };

        public override int SpawnRate => 90;

        public override int Music => MusicID.PirateInvasion;

        public override int MaxSpawns => 3;

        public override bool CanStart() => !Main.dayTime;

        public override bool ShouldFail() => Main.dayTime;
    }
}
