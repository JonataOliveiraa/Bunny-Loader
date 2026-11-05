using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems
{

    public class ExampleGameTipsSystem : ModSystem
    {

        public override void ModifyGameTipVisibility(IReadOnlyList<GameTipData> gameTips) {

            if (GameTipID.BloodMoonZombieDoorOpening < gameTips.Count) gameTips[GameTipID.BloodMoonZombieDoorOpening].Hide();
            if (GameTipID.SolarEclipseCreepyMonsters < gameTips.Count) gameTips[GameTipID.SolarEclipseCreepyMonsters].Hide();

            GameTipData disabledTip = gameTips.FirstOrDefault(tip => tip.FullName == "ExampleMod/DisabledExampleTip");

            disabledTip?.Hide();
        }
    }
}
