using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsHair.Content.Hairs;

public class TestHair : ModHair
{
    public static bool Unlock;
    public static int StaticDefaults;

    public override bool AvailableDuringCharacterCreation => false;

    public override void SetStaticDefaults()
    {
        StaticDefaults++;
        HairID.Sets.DrawBackHair[Type] = true;
    }

    public override IEnumerable<Condition> GetUnlockConditions()
    {
        yield return new Condition("Mods.CsHair.Conditions.Unlock", () => Unlock);
    }
}
