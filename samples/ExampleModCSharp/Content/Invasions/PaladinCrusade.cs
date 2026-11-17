using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Invasions
{
    // A short, single wave invasion of Paladins, started by using a ConsecratedAxe. See GraveLegion for a longer one with more hooks.
    public class PaladinCrusade : ModInvasion
    {
        public override Color MessageColor => new Color(255, 215, 90);

        // 12 Paladin kills clear the only wave.
        public override IList<InvasionWave> Waves { get; } = new[] { new InvasionWave(12, new InvasionEnemy(NPCID.Paladin)) };

        public override int SpawnRate => 90;

        public override int Music => MusicID.PirateInvasion;

        // Paladins are strong, so only a few of them are allowed at the same time.
        public override int MaxSpawns => 3;

        // This invasion can only be started at night...
        public override bool CanStart() => !Main.dayTime;

        // ...and fails if the sun rises before it is cleared, like the Pumpkin Moon and Frost Moon.
        public override bool ShouldFail() => Main.dayTime;
    }
}
