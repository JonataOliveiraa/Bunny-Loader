using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Hairs;

public class ExampleHair : ModHair
{
    public override Gender RandomizedCharacterCreationGender => Gender.Female;

    public override bool AvailableDuringCharacterCreation => false;

    public override void SetStaticDefaults()
    {
        HairID.Sets.DrawBackHair[Type] = true;
    }

    public override IEnumerable<Condition> GetUnlockConditions()
    {
        yield return Condition.TimeNight;
    }
}
